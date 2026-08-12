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
    public int GetBatchSize() => 10;

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

        var fileEntries = new ConcurrentBag<FileEntry>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken,
        };

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

        await Parallel.ForEachAsync(
            requests,
            parallelOptions,
            async (request, ct) =>
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

                    fileEntries.Add(fileEntry);
                }
                catch (NotSupportedException)
                {
                    fileEntry.ProcessStatus = FileProcessStatus.NotSupportedFile;
                    fileEntries.Add(fileEntry);
                    logger.FileNotSupported(request.FilePath, Path.GetExtension(request.FilePath));
                    result = "not_supported";
                }
                catch (Exception ex)
                {
                    fileEntry.ProcessStatus = FileProcessStatus.TextExtractionFailed;
                    fileEntry.Content = ex.Message;
                    fileEntries.Add(fileEntry);
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
        );

        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await dbContext.BulkUpdateAsync(
                    fileEntries.ToList(),
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

        var extracted = fileEntries.Count(e => e.ProcessStatus == FileProcessStatus.TextExtracted);
        var empty = fileEntries.Count(e => e.ProcessStatus == FileProcessStatus.EmptyContent);
        var notSupported = fileEntries.Count(e => e.ProcessStatus == FileProcessStatus.NotSupportedFile);
        var failed = fileEntries.Count(e => e.ProcessStatus == FileProcessStatus.TextExtractionFailed);

        logger.TextExtractFinished(extracted, empty, notSupported, failed);

        if (extracted > 0)
        {
            logger.StageHandoff(extracted, "FileClassify");
            var traceParent = TracingHelper.CaptureTraceParent();
            foreach (var entry in fileEntries.Where(e => e.ProcessStatus == FileProcessStatus.TextExtracted))
            {
                await fileClassifyChannel.Writer.WriteAsync(
                    new FileClassifyRequest(entry.Id, traceParent),
                    cancellationToken
                );
            }
        }
    }
}