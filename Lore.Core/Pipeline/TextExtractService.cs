using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;
using Lore.Core.Retrieval;
using Lore.Core.Telemetry;
using Lore.Core.TextExtractors;
using Lore.Data;
using Lore.Data.Models;

namespace Lore.Core.Pipeline;

public class TextExtractService(
    ILogger<TextExtractService> logger,
    ITextExtractorFactory textExtractorFactory,
    Channel<FileClassifyRequest> fileClassifyChannel,
    IDbContextFactory<LoreDbContext> dbContextFactory
) : IChannelService<TextExtractRequest>
{
    public int GetBatchSize() => 25;

    public async Task ProcessAsync(
        TextExtractRequest request,
        CancellationToken cancellationToken
    ) => await ProcessBatchAsync([request], cancellationToken);

    public async Task ProcessBatchAsync(
        IReadOnlyList<TextExtractRequest> requests,
        CancellationToken cancellationToken
    )
    {
        logger.TextExtractStarted(requests.Count);

        var fileEntries = new ConcurrentBag<(FileEntry Entry, string? TraceParent)>();

        Dictionary<string, int> fileEntryIds = [];
        var incomingPaths = requests.Select(req => req.FilePath).Distinct();
        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            fileEntryIds = await dbContext
                .Files.AsNoTracking()
                .Where(fl => incomingPaths.Contains(fl.Path))
                .Select(fl => new { fl.Path, fl.Id })
                .ToDictionaryAsync(fl => fl.Path, fl => fl.Id, cancellationToken);
        }

        using var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        var tasks = requests.Select(async request =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var activity = TracingHelper.StartStageSpan("textextract", request.FilePath, request.TraceParent);

                if (!fileEntryIds.TryGetValue(request.FilePath, out var fileId))
                {
                    logger.FileMissingInDb(request.FilePath);
                    return;
                }

                activity?.SetTag("file.id", fileId);

                var fileEntry = new FileEntry
                {
                    Id = fileEntryIds[request.FilePath],
                    ProcessStatus = FileProcessStatus.Pending,

                    Path = default!,
                    Name = default!,
                    Hash = default!,
                    Extension = default!,
                    Directory = default!,
                };

                var sw = Stopwatch.StartNew();
                string? result;

                try
                {
                    var extractor = textExtractorFactory.GetExtractor(request.FilePath);
                    var extractedText = await extractor.ExtractTextAsync(request.FilePath);
                    var cleanedText = extractedText.CleanTextForRAG();
                    if (string.IsNullOrWhiteSpace(cleanedText))
                    {
                        fileEntry.ProcessStatus = FileProcessStatus.EmptyContent;
                        logger.ExtractionEmpty(request.FilePath);
                        result = "empty";
                    }
                    else
                    {
                        fileEntry.ProcessStatus = FileProcessStatus.TextExtracted;
                        fileEntry.Content = cleanedText;
                        logger.ExtractionOutcome(request.FilePath, extractor.GetType().Name, cleanedText.Length);
                        result = "extracted";
                    }

                    fileEntries.Add((fileEntry, Activity.Current?.Id));
                }
                catch (NotSupportedException)
                {
                    fileEntry.ProcessStatus = FileProcessStatus.NotSupportedFile;
                    fileEntries.Add((fileEntry, Activity.Current?.Id));
                    logger.FileNotSupported(request.FilePath, Path.GetExtension(request.FilePath));
                    result = "not_supported";
                }
                catch (Exception ex)
                {
                    fileEntry.ProcessStatus = FileProcessStatus.TextExtractionFailed;
                    fileEntry.Content = ex.Message;
                    fileEntries.Add((fileEntry, Activity.Current?.Id));
                    logger.ExtractionFailed(request.FilePath, ex);
                    result = "failed";
                }

                sw.Stop();
                LoreMetrics.PipelineFilesProcessed.Add(1,
                    new KeyValuePair<string, object?>("pipeline.stage", "textextract"),
                    new KeyValuePair<string, object?>("result", result));
                LoreMetrics.PipelineFileDuration.Record(sw.ElapsedMilliseconds,
                    new KeyValuePair<string, object?>("pipeline.stage", "textextract"));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var fileEntryList = fileEntries.Select(x => x.Entry).ToList();

        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await dbContext.BulkUpdateAsync(
                    fileEntryList,
                    new BulkConfig
                    {
                        PropertiesToInclude =
                        [
                            nameof(FileEntry.Content),
                            nameof(FileEntry.ProcessStatus),
                        ],
                    },
                    cancellationToken: cancellationToken
                );
            });
        }

        var extracted = fileEntryList.Count(e => e.ProcessStatus == FileProcessStatus.TextExtracted);
        var empty = fileEntryList.Count(e => e.ProcessStatus == FileProcessStatus.EmptyContent);
        var notSupported = fileEntryList.Count(e => e.ProcessStatus == FileProcessStatus.NotSupportedFile);
        var failed = fileEntryList.Count(e => e.ProcessStatus == FileProcessStatus.TextExtractionFailed);

        logger.TextExtractFinished(extracted, empty, notSupported, failed);

        if (extracted > 0)
        {
            logger.StageHandoff(extracted, "FileClassify");
            foreach (var (entry, traceParent) in fileEntries.Where(e => e.Entry.ProcessStatus == FileProcessStatus.TextExtracted))
            {
                await fileClassifyChannel.Writer.WriteAsync(
                    new FileClassifyRequest(entry.Id, traceParent),
                    cancellationToken
                );
            }
        }
    }
}