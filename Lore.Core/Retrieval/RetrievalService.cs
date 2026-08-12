using System.Text;
using System.Text.Json;
using Lore.Common.Models;
using Lore.Core.Settings;
using Lore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartComponents.LocalEmbeddings;

namespace Lore.Core.Retrieval;

public class RetrievalService(
    ILogger<RetrievalService> logger,
    IUserSettingsService userSettings,
    LocalEmbedder embedder,
    IDbContextFactory<LoreDbContext> dbContextFactory
) : IRetrievalService
{
    public async Task<List<DocumentChunkFile>> GetChunkContentsAsync(List<int> documentChunkIds, CancellationToken cancellationToken)
    {
        if (documentChunkIds.Count == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(cancellationToken);
        var rawChunks = await dbContext
                        .FileChunks.AsNoTracking()
                        .Where(c => documentChunkIds.Contains(c.Id))
                        .Select(c => new
                        {
                            c.Id,
                            c.ChunkText,
                            c.ChunkIndex,
                            c.FileEntryId,
                            FilePath = c.FileEntry.Path,
                            CategoryName = c.FileEntry != null && c.FileEntry.PrimaryCategory != null
                                ? c.FileEntry.PrimaryCategory.Name
                                : null,
                            DocTypeName = c.FileEntry != null && c.FileEntry.DocumentType != null
                                ? c.FileEntry.DocumentType.Name
                                : null,
                        })
                        .ToListAsync(cancellationToken);

        var chunksByFile = rawChunks.GroupBy(c => new
        {
            c.FileEntryId,
            c.CategoryName,
            c.DocTypeName,
            c.FilePath
        }).Select(cf => new DocumentChunkFile(
            cf.Key.FileEntryId,
            cf.Key.FilePath,
            cf.Key.CategoryName,
            cf.Key.DocTypeName,
            [.. cf.Select(ch => new DocumentChunk(
                ch.Id,
                ch.ChunkText,
                ch.ChunkIndex))]));

        return [.. chunksByFile];
    }

    public async Task<List<int>> RetrieveDocumentChunksAsync(
    RetrievalQuery query,
    CancellationToken cancellationToken
)
    {
        var formattedFts = FormatFtsQuery(query.FTSTerms);
        var cleanedPassage = CleanSearchQuery(query.SearchQuery);
        var maxNumberSearchResults = userSettings.GetSetting<int>(UserSettingsType.MaxNumberSearchResults);

        await using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(cancellationToken);

        Task<List<int>> ftsTask = !string.IsNullOrWhiteSpace(formattedFts)
            ? dbContext
                .Database.SqlQuery<int>(
                    $"SELECT rowid FROM file_chunks_fts WHERE file_chunks_fts MATCH {formattedFts} ORDER BY rank LIMIT {maxNumberSearchResults}"
                )
                .ToListAsync(cancellationToken)
            : Task.FromResult(new List<int>());

        var embedding = embedder.Embed(cleanedPassage).Values.ToArray();
        var passageVectorJson = JsonSerializer.Serialize(embedding);
        var chunkVectorTask = dbContext
            .Database.SqlQuery<int>(
                $"SELECT chunk_id FROM vec_file_chunks WHERE embedding MATCH {passageVectorJson} AND k = {maxNumberSearchResults} ORDER BY distance ASC"
            )
            .ToListAsync(cancellationToken);

        await Task.WhenAll(ftsTask, chunkVectorTask);
        var ftsResults = await ftsTask;
        var vectorResults = await chunkVectorTask;

        const int k = 60;
        var rrfScores = new Dictionary<int, double>();

        void ProcessStream(List<int> stream, double weight)
        {
            for (int rank = 0; rank < stream.Count; rank++)
            {
                int chunkId = stream[rank];
                double score = weight * (1.0 / (k + rank + 1));

                if (!rrfScores.TryAdd(chunkId, score))
                {
                    rrfScores[chunkId] += score;
                }
            }
        }

        // Apply weights per retrieval stream
        ProcessStream(ftsResults, userSettings.GetSetting<float>(UserSettingsType.SearchFTSWeight));
        ProcessStream(vectorResults, userSettings.GetSetting<float>(UserSettingsType.SearchVectorWeight));

        return rrfScores
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .Take(maxNumberSearchResults)
            .ToList();
    }

    private static string CleanSearchQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return string.Empty;
        }

        return string.Join(
            " ",
            query
                .Trim()
                .Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));
    }

    private static string FormatFtsQuery(IEnumerable<string> input)
    {
        var terms = input
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (terms.Length == 0)
        {
            return string.Empty;
        }

        static string EscapeFts5Phrase(string value)
        {
            // FTS5 phrase syntax uses double quotes.
            // A literal double quote inside the phrase is escaped by doubling it.
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return string.Join(
            " OR ",
            terms.Select(EscapeFts5Phrase));
    }
}