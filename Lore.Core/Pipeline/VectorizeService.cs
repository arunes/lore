using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;
using Lore.Core.Telemetry;
using Lore.Data;
using Lore.Data.Models;
using SmartComponents.LocalEmbeddings;

namespace Lore.Core.Pipeline;

public class VectorizeService(
    ILogger<VectorizeService> logger,
    IDbContextFactory<LoreDbContext> dbContextFactory,
    LocalEmbedder embedder
) : IChannelService<VectorizeRequest>
{
    public int GetBatchSize() => 10;

    public async Task ProcessAsync(VectorizeRequest request, CancellationToken cancellationToken) =>
        await ProcessBatchAsync([request], cancellationToken);

    private record ChunkInformation(
        int Id,
        string FileName,
        string Directory,
        string ChunkText,
        string? PrimaryCategory,
        string? DocumentType
    );

    private record ChunkVectorInformation(int Id, ReadOnlyMemory<float> Vector);

    public async Task ProcessBatchAsync(
        IReadOnlyList<VectorizeRequest> requests,
        CancellationToken cancellationToken
    )
    {
        logger.VectorizeStarted(requests.Count);

        var incomingIds = requests.Select(req => req.FileId).Distinct().ToList();
        var fileChunkContents = await GetFileChunkContents(incomingIds, cancellationToken);

        var vectorizedResults = new ConcurrentDictionary<int, List<ChunkVectorInformation>>();
        var perFileDurations = new ConcurrentDictionary<int, long>();

        using var semaphore = new SemaphoreSlim(2);
        var sw = Stopwatch.StartNew();

        var tasks = fileChunkContents.Select(async fcc =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await Task.Run(() =>
                {
                    var fileSw = Stopwatch.StartNew();

                    var fileId = fcc.Key;
                    var traceParent = requests.FirstOrDefault(r => r.FileId == fileId)?.TraceParent;
                    using var activity = TracingHelper.StartStageSpan("vectorize", fileId.ToString(), traceParent);

                    vectorizedResults[fcc.Key] = GetVectorsForChunks(fcc.Value, activity);

                    fileSw.Stop();
                    perFileDurations[fcc.Key] = fileSw.ElapsedMilliseconds;

                    activity?.SetTag("file.vector_count", vectorizedResults[fcc.Key].Count);
                    LoreMetrics.PipelineFilesProcessed.Add(1,
                        new KeyValuePair<string, object?>("pipeline.stage", "vectorize"),
                        new KeyValuePair<string, object?>("result", "success"));
                    LoreMetrics.PipelineFileDuration.Record(fileSw.ElapsedMilliseconds,
                        new KeyValuePair<string, object?>("pipeline.stage", "vectorize"));
                });
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        sw.Stop();
        logger.LogDebug("All embeddings created for {TotalFiles} files in {TotalMilliseconds}ms",
            fileChunkContents.Count, sw.ElapsedMilliseconds);

        foreach (var (fileId, chunks) in vectorizedResults)
        {
            perFileDurations.TryGetValue(fileId, out var durationMs);
            logger.FileVectorized(fileId, chunks.Count, durationMs);
        }

        await using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(
            cancellationToken
        );

        int totalVectorsWritten = 0;

        foreach (var (fileId, chunks) in vectorizedResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (chunks.Count == 0)
            {
                logger.ZeroVectorsWritten(fileId);
                continue;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                cancellationToken
            );

            try
            {
                var chunkIds = chunks.Select(ch => ch.Id).ToList();
                await dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM vec_file_chunks WHERE chunk_id IN (SELECT value FROM json_each({0}));",
                    [JsonSerializer.Serialize(chunkIds)],
                    cancellationToken
                );

                foreach (var chunkBatch in chunks.Chunk(25))
                {
                    var sqlBuilder = new StringBuilder(
                        "INSERT INTO vec_file_chunks(chunk_id, embedding) VALUES "
                    );
                    var parameters = new List<object>();

                    for (int i = 0; i < chunkBatch.Length; i++)
                    {
                        if (i > 0)
                            sqlBuilder.Append(", ");

                        var paramIndexId = parameters.Count;
                        parameters.Add(chunkBatch[i].Id);

                        var paramIndexVec = parameters.Count;
                        parameters.Add(JsonSerializer.Serialize(chunkBatch[i].Vector.ToArray()));

                        sqlBuilder.Append($"({{{paramIndexId}}}, {{{paramIndexVec}}})");
                    }

                    await dbContext.Database.ExecuteSqlRawAsync(
                        sqlBuilder.ToString(),
                        parameters.ToArray(),
                        cancellationToken
                    );
                }

                await dbContext
                    .Files.Where(fl => fl.Id == fileId)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(e => e.ProcessStatus, e => FileProcessStatus.Done),
                        cancellationToken
                    );

                await transaction.CommitAsync(cancellationToken);
                totalVectorsWritten += chunks.Count;
            }
            catch (Exception ex)
            {
                logger.VectorWriteFailed(fileId, ex);
            }
        }

        logger.VectorizeFinished(vectorizedResults.Count, totalVectorsWritten);
    }

    private List<ChunkVectorInformation> GetVectorsForChunks(List<ChunkInformation> chunks, Activity? activity)
    {
        var inputs = chunks
            .Select(ch =>
            {
                var sb = new StringBuilder();

                sb.Append("File: ").AppendLine(ch.FileName);
                sb.Append("Directory: ").AppendLine(ch.Directory);

                if (!string.IsNullOrWhiteSpace(ch.PrimaryCategory))
                    sb.Append("Primary Category: ").AppendLine(ch.PrimaryCategory);

                if (!string.IsNullOrWhiteSpace(ch.DocumentType))
                    sb.Append("Document Type: ").AppendLine(ch.DocumentType);

                sb.AppendLine().Append(ch.ChunkText);

                return (Chunk: ch, Text: sb.ToString());
            })
            .ToList();

        var sw = Stopwatch.StartNew();
        var embeddings = embedder.EmbedRange(inputs, x => x.Text);
        sw.Stop();

        activity?.SetTag("embedding.count", inputs.Count);
        activity?.SetTag("embedding.duration_ms", sw.ElapsedMilliseconds);

        return embeddings
            .Select(x => new ChunkVectorInformation(x.Item.Chunk.Id, x.Embedding.Values))
            .ToList();
    }

    private async Task<Dictionary<int, List<ChunkInformation>>> GetFileChunkContents(
        IReadOnlyCollection<int> incomingIds,
        CancellationToken cancellationToken
    )
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var items = await dbContext
            .FileChunks.AsNoTracking()
            .Where(fl => incomingIds.Contains(fl.FileEntryId))
            .Select(fc => new
            {
                fc.Id,
                fc.FileEntryId,
                fc.ChunkText,
                FileName = fc.FileEntry.Path,
                Directory = fc.FileEntry.Directory,
                PrimaryCategory = fc.FileEntry.PrimaryCategory != null
                    ? fc.FileEntry.PrimaryCategory.Name
                    : null,
                DocumentType = fc.FileEntry.DocumentType != null
                    ? fc.FileEntry.DocumentType.Name
                    : null,
            })
            .ToListAsync(cancellationToken);

        return items
            .GroupBy(fc => fc.FileEntryId)
            .ToDictionary(
                grp => grp.Key,
                grp =>
                    grp.Select(fc => new ChunkInformation(
                            fc.Id,
                            fc.FileName,
                            fc.Directory,
                            fc.ChunkText,
                            fc.PrimaryCategory,
                            fc.DocumentType
                        ))
                        .ToList()
            );
    }
}