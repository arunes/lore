using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;
using Lore.Core.Retrieval;
using Lore.Core.Telemetry;
using Lore.Data;
using Lore.Data.Models;

namespace Lore.Core.Pipeline;

public class FileClassifyService(
    ILogger<FileClassifyService> logger,
    IDbContextFactory<LoreDbContext> dbContextFactory,
    Channel<ChunkingRequest> chunkingChannel,
    EmbeddingCache embeddingCache
) : IChannelService<FileClassifyRequest>
{
    public int GetBatchSize() => 100;

    public async Task ProcessAsync(
        FileClassifyRequest request,
        CancellationToken cancellationToken
    ) => await ProcessBatchAsync([request], cancellationToken);

    private record FileInformation(
        int Id,
        string Name,
        string Directory,
        string Extension,
        string? Snippet,
        DateTime CreatedAt,
        DateTime ModifiedAt
    );

    public async Task ProcessBatchAsync(
        IReadOnlyList<FileClassifyRequest> requests,
        CancellationToken cancellationToken
    )
    {
        logger.ClassifyStarted(requests.Count);

        var incomingIds = requests.Select(req => req.FileId).Distinct().ToList();
        Dictionary<int, FileInformation> fileEntryContents;

        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            fileEntryContents = await dbContext
                .Files.AsNoTracking()
                .Where(fl => incomingIds.Contains(fl.Id))
                .Select(fl => new FileInformation(
                    fl.Id,
                    fl.Name,
                    fl.Directory,
                    fl.Extension,
                    fl.Content != null ? fl.Content.Substring(0, 1000) : null,
                    fl.FileCreatedAt,
                    fl.FileModifiedAt
                ))
                .ToDictionaryAsync(fl => fl.Id, fl => fl, cancellationToken);
        }

        var fileEntries = new ConcurrentBag<(FileEntry Entry, string? TraceParent)>();
        using var semaphore = new SemaphoreSlim(4);
        var tasks = requests.Select(async request =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var activity = TracingHelper.StartStageSpan("classify", request.FileId.ToString(), request.TraceParent);

                if (!fileEntryContents.TryGetValue(request.FileId, out var fileInfo))
                {
                    logger.ClassifyFileMissing(request.FileId);
                    return;
                }

                activity?.SetTag("file.path", fileInfo.Name);

                var fileEntry = new FileEntry
                {
                    Id = request.FileId,
                    ProcessStatus = default!,
                    Path = string.Empty,
                    Name = string.Empty,
                    Hash = string.Empty,
                    Extension = string.Empty,
                    Directory = string.Empty,
                };

                var sw = Stopwatch.StartNew();
                string? result;

                try
                {
                    var classificationInput = BuildClassificationInput(
                        fileInfo.Name,
                        fileInfo.Directory,
                        fileInfo.Extension,
                        fileInfo.Snippet
                    );
                    var primaryCategory = embeddingCache.FindBestCategory(classificationInput);
                    var documentType = embeddingCache.FindBestDocumentType(classificationInput);

                    fileEntry.PrimaryCategoryId = primaryCategory?.Id;
                    fileEntry.DocumentTypeId = documentType?.Id;
                    fileEntry.ProcessStatus = FileProcessStatus.Classified;
                    result = "classified";

                    activity?.SetTag("file.category", primaryCategory?.Name ?? "none");
                    activity?.SetTag("file.doc_type", documentType?.Name ?? "none");

                    if (primaryCategory == null && documentType == null)
                    {
                        logger.NoCategoryMatched(request.FileId);
                    }
                    else
                    {
                        logger.FileClassified(
                            request.FileId,
                            fileInfo.Name,
                            primaryCategory?.Name ?? "none",
                            documentType?.Name ?? "none"
                        );
                    }
                }
                catch (Exception ex)
                {
                    fileEntry.ProcessStatus = FileProcessStatus.ClassificationFailed;
                    logger.ClassifyFailed(request.FileId, ex);
                    result = "failed";
                }

                sw.Stop();
                LoreMetrics.PipelineFilesProcessed.Add(1,
                    new KeyValuePair<string, object?>("pipeline.stage", "classify"),
                    new KeyValuePair<string, object?>("result", result));
                LoreMetrics.PipelineFileDuration.Record(sw.ElapsedMilliseconds,
                    new KeyValuePair<string, object?>("pipeline.stage", "classify"));

                fileEntries.Add((fileEntry, Activity.Current?.Id));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                var classifiedGroups = fileEntries
                    .Where(e => e.Entry.ProcessStatus == FileProcessStatus.Classified)
                    .GroupBy(e => new { e.Entry.PrimaryCategoryId, e.Entry.DocumentTypeId });

                foreach (var group in classifiedGroups)
                {
                    var ids = group.Select(e => e.Entry.Id).ToList();
                    await dbContext
                        .Files.Where(f => ids.Contains(f.Id))
                        .ExecuteUpdateAsync(
                            s =>
                                s.SetProperty(f => f.PrimaryCategoryId, group.Key.PrimaryCategoryId)
                                    .SetProperty(f => f.DocumentTypeId, group.Key.DocumentTypeId)
                                    .SetProperty(
                                        f => f.ProcessStatus,
                                        FileProcessStatus.Classified
                                    ),
                            cancellationToken
                        );
                }

                var failedIds = fileEntries
                    .Where(e => e.Entry.ProcessStatus == FileProcessStatus.ClassificationFailed)
                    .Select(e => e.Entry.Id)
                    .ToList();

                if (failedIds.Count > 0)
                {
                    await dbContext
                        .Files.Where(f => failedIds.Contains(f.Id))
                        .ExecuteUpdateAsync(
                            s =>
                                s.SetProperty(
                                    f => f.ProcessStatus,
                                    FileProcessStatus.ClassificationFailed
                                ),
                            cancellationToken
                        );
                }
            });
        }

        var classified = fileEntries.Count(e => e.Entry.ProcessStatus == FileProcessStatus.Classified);
        var failed = fileEntries.Count(e => e.Entry.ProcessStatus == FileProcessStatus.ClassificationFailed);
        logger.ClassifyFinished(classified, failed);

        logger.StageHandoff(fileEntries.Count, "Chunking");
        foreach (var (entry, traceParent) in fileEntries)
        {
            await chunkingChannel.Writer.WriteAsync(
                new ChunkingRequest(entry.Id, traceParent),
                cancellationToken
            );
        }
    }

    private static string BuildClassificationInput(
        string fileName,
        string directory,
        string extension,
        string? fileContentSnippet
    )
    {
        return $"""
            File Name: {fileName}
            File Type/Extension: {extension}
            Directory Path: {directory}

            Document Snippet:
            {fileContentSnippet ?? "Empty"}
            """;
    }
}