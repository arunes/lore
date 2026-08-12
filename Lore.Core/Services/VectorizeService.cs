using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lore.Data;
using Lore.Data.Models;
using SmartComponents.LocalEmbeddings;

namespace Lore.Core.Services;

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
        logger.LogInformation("Starting Vectorize process for {TotalFiles} files", requests.Count);

        var incomingIds = requests.Select(req => req.FileId).Distinct().ToList();
        var fileChunkContents = await GetFileChunkContents(incomingIds, cancellationToken);

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 2,
            CancellationToken = cancellationToken,
        };

        var vectorizedResults = new ConcurrentDictionary<int, List<ChunkVectorInformation>>();

        var sw = Stopwatch.StartNew();
        Parallel.ForEach(
            fileChunkContents,
            parallelOptions,
            fcc =>
            {
                vectorizedResults[fcc.Key] = GetVectorsForChunks(fcc.Value);
            }
        );

        sw.Stop();
        logger.LogWarning(
            "All embeddings created for {TotalFiles} file, taking {TotalMilliseconds} ms.",
            fileChunkContents.Count,
            sw.ElapsedMilliseconds
        );

        await using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(
            cancellationToken
        );

        foreach (var (fileId, chunks) in vectorizedResults)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (chunks.Count == 0)
                continue;

            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                cancellationToken
            );

            try
            {
                // Delete existing vectors using safe parameterized JSON expression
                var chunkIds = chunks.Select(ch => ch.Id).ToList();
                await dbContext.Database.ExecuteSqlRawAsync(
                    "DELETE FROM vec_file_chunks WHERE chunk_id IN (SELECT value FROM json_each({0}));",
                    [JsonSerializer.Serialize(chunkIds)],
                    cancellationToken
                );

                // Parameterized INSERT statements in batches
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
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed writing vector embeddings to SQLite for File ID {FileId}",
                    fileId
                );
            }
        }

        logger.LogInformation(
            "Vectorizing process finished, processed {FileCount} records",
            requests.Count
        );
    }

    private List<ChunkVectorInformation> GetVectorsForChunks(List<ChunkInformation> chunks)
    {
        var sw = Stopwatch.StartNew();
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

        var embeddings = embedder.EmbedRange(inputs, x => x.Text);
        var result = embeddings
            .Select(x => new ChunkVectorInformation(x.Item.Chunk.Id, x.Embedding.Values))
            .ToList();

        sw.Stop();

        logger.LogWarning(
            "Embeddings created for {TotalChunks} chunks, taking {TotalMilliseconds} ms.",
            chunks.Count,
            sw.ElapsedMilliseconds
        );

        return result;
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
