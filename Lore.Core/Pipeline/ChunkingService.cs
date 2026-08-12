using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Text;
using Lore.Core.Logging;
using Lore.Core.Telemetry;
using Lore.Data;
using Lore.Data.Models;

namespace Lore.Core.Pipeline;

public class ChunkingService(
    ILogger<ChunkingService> logger,
    Channel<VectorizeRequest> vectorizeChannel,
    IDbContextFactory<LoreDbContext> dbContextFactory
) : IChannelService<ChunkingRequest>
{
    public int GetBatchSize() => 25;

    public async Task ProcessAsync(ChunkingRequest request, CancellationToken cancellationToken) =>
        await ProcessBatchAsync([request], cancellationToken);

    public async Task ProcessBatchAsync(
        IReadOnlyList<ChunkingRequest> requests,
        CancellationToken cancellationToken
    )
    {
        logger.ChunkingStarted(requests.Count);
        var fileChunkEntries = new ConcurrentBag<FileEntryChunk>();
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken,
        };

        Dictionary<int, string?> fileEntryContents = [];
        var incomingIds = requests.Select(req => req.FileId).Distinct();
        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            fileEntryContents = await dbContext
                .Files.AsNoTracking()
                .Where(fl => incomingIds.Contains(fl.Id) && !string.IsNullOrWhiteSpace(fl.Content))
                .Select(fl => new { fl.Id, fl.Content })
                .ToDictionaryAsync(fl => fl.Id, fl => fl.Content, cancellationToken);
        }

        var chunkedFiles = new ConcurrentDictionary<int, int>();

        await Parallel.ForEachAsync(
            requests,
            parallelOptions,
            async (request, ct) =>
            {
                using var activity = TracingHelper.StartStageSpan("chunking", request.FileId.ToString(), request.TraceParent);

                if (!fileEntryContents.TryGetValue(request.FileId, out var fileContent))
                {
                    logger.ChunkingFileMissing(request.FileId);
                    return;
                }

                var sw = Stopwatch.StartNew();

                var chunks = ChunkText(fileContent!);
                chunkedFiles.TryAdd(request.FileId, chunks.Count);

                activity?.SetTag("file.chunk_count", chunks.Count);

                for (int i = 0; i < chunks.Count; i++)
                {
                    fileChunkEntries.Add(
                        new FileEntryChunk
                        {
                            FileEntryId = request.FileId,
                            ChunkIndex = i,
                            ChunkText = chunks[i],
                        }
                    );
                }

                sw.Stop();
                LoreMetrics.PipelineFilesProcessed.Add(1,
                    new KeyValuePair<string, object?>("pipeline.stage", "chunking"),
                    new KeyValuePair<string, object?>("result", "success"));
                LoreMetrics.PipelineFileDuration.Record(sw.ElapsedMilliseconds,
                    new KeyValuePair<string, object?>("pipeline.stage", "chunking"));
            }
        );

        foreach (var (fileId, chunkCount) in chunkedFiles)
        {
            logger.FileChunked(fileId, chunkCount, 0);
        }

        var distinctFileEntryIds = fileChunkEntries.Select(ce => ce.FileEntryId).Distinct();
        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await dbContext
                    .Files.Where(fl => distinctFileEntryIds.Contains(fl.Id))
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(e => e.ProcessStatus, e => FileProcessStatus.ChunksCreated)
                    );

                await dbContext.BulkInsertAsync(
                    fileChunkEntries,
                    new BulkConfig { },
                    cancellationToken: cancellationToken
                );
            });
        }

        var fileCount = distinctFileEntryIds.Count();
        logger.ChunkingFinished(fileCount, fileChunkEntries.Count);

        if (fileCount > 0)
        {
            logger.StageHandoff(fileCount, "Vectorize");
            var traceParent = TracingHelper.CaptureTraceParent();
            foreach (var fileEntryId in distinctFileEntryIds)
            {
                await vectorizeChannel.Writer.WriteAsync(
                    new VectorizeRequest(fileEntryId, traceParent),
                    cancellationToken
                );
            }
        }
    }

    public static List<string> ChunkText(string input)
    {
#pragma warning disable SKEXP0050
        var lines = TextChunker.SplitPlainTextLines(input, 100);
        return TextChunker.SplitPlainTextParagraphs(lines, 300, 30);
#pragma warning restore SKEXP0050
    }
}