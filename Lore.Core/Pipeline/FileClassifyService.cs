using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lore.Core.Retrieval;
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
        logger.LogInformation(
            "Starting FileClassify process for {TotalFiles} files",
            requests.Count
        );

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

        var fileEntries = new ConcurrentBag<FileEntry>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(
            requests,
            parallelOptions,
            async (request, ct) =>
            {
                if (!fileEntryContents.TryGetValue(request.FileId, out var fileInfo))
                {
                    logger.LogWarning(
                        "File ID {FileId} not found or empty. Skipping classification.",
                        request.FileId
                    );
                    return;
                }

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
                }
                catch (Exception ex)
                {
                    fileEntry.ProcessStatus = FileProcessStatus.ClassificationFailed;
                    logger.LogError(ex, "Failed to classify File ID {FileId}", request.FileId);
                }

                fileEntries.Add(fileEntry);
            }
        );

        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                // 1. Group classified files by Category and Type pair
                var classifiedGroups = fileEntries
                    .Where(e => e.ProcessStatus == FileProcessStatus.Classified)
                    .GroupBy(e => new { e.PrimaryCategoryId, e.DocumentTypeId });

                foreach (var group in classifiedGroups)
                {
                    var ids = group.Select(e => e.Id).ToList();
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
                    .Where(e => e.ProcessStatus == FileProcessStatus.ClassificationFailed)
                    .Select(e => e.Id)
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

        logger.LogInformation("Classification finished for {Count} files.", fileEntries.Count);

        foreach (var entry in fileEntries)
        {
            await chunkingChannel.Writer.WriteAsync(
                new ChunkingRequest(entry.Id),
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
