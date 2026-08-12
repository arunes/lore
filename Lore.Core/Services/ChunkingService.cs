using System.Collections.Concurrent;
using System.Threading.Channels;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Text;
using Lore.Data;
using Lore.Data.Models;

namespace Lore.Core.Services;

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
        logger.LogInformation("Starting Chunking process for {TotalFiles} files", requests.Count);
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

        await Parallel.ForEachAsync(
            requests,
            parallelOptions,
            async (request, ct) =>
            {
                if (!fileEntryContents.TryGetValue(request.FileId, out var fileContent))
                {
                    logger.LogWarning(
                        "File ID {FileId} not found or empty. Skipping chunking.",
                        request.FileId
                    );
                    return;
                }

                var chunks = ChunkText(fileContent!);
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
            }
        );

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

        logger.LogInformation(
            "Chunking process finished, processed {FileCount} records",
            fileChunkEntries.Count
        );

        foreach (var fileEntryId in distinctFileEntryIds)
        {
            await vectorizeChannel.Writer.WriteAsync(
                new VectorizeRequest(fileEntryId),
                cancellationToken
            );
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
