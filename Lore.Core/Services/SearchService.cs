using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Lore.Common.Helpers;
using Lore.Common.Models;
using Lore.Core.LLM;
using Lore.Data;
using SmartComponents.LocalEmbeddings;

namespace Lore.Core.Services;

public record StreamingSearchContextResult(
    List<int> TopChunkIds,
    string FormattedContext,
    IAsyncEnumerable<string> LLMResponseStream,
    RefinedQuery Query
);

public interface ISearchService
{
    /// <summary>
    /// Executes multi-stream search (FTS + Vector), merges results with RRF,
    /// retrieves context, and streams the LLM response tokens as they arrive.
    /// </summary>
    Task<StreamingSearchContextResult> SearchAsync(
        string query,
        bool refineQuery = false,
        CancellationToken cancellationToken = default
    );
}

public class SearchService(
    ILogger<SearchService> logger,
    LocalEmbedder embedder,
    IDbContextFactory<LoreDbContext> dbContextFactory,
    IChatClientFactory chatClientFactory
) : ISearchService
{
    private static readonly ChatOptions ChatOptions = new()
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema(
            schema: AIJsonUtilities.CreateJsonSchema(typeof(RefinedQuery)),
            schemaName: "document_metadata_schema"
        ),
        Temperature = 0.1f,
    };

    public async Task<StreamingSearchContextResult> SearchAsync(
        string query,
        bool refineQuery = false,
        CancellationToken cancellationToken = default
    )
    {
        // Note: Client ownership is passed to the streaming enumerator
        var chatClient = await chatClientFactory.CreateClientAsync(cancellationToken);

        var sanitized = query.Replace("'", "''").Replace("\"", "").Trim();
        RefinedQuery? userQuery = null;

        if (refineQuery)
        {
            userQuery = await GetRefinedQueryAsync(chatClient, query, cancellationToken);
        }

        userQuery = ValidateAndSanitizeRefinedQuery(userQuery, sanitized);
        var topChunkIds = await SearchAndMergeAsync(userQuery, cancellationToken);
        var formattedContext = await GetFormattedContextAsync(topChunkIds, cancellationToken);
        var synthesisQuery = string.IsNullOrWhiteSpace(query) ? userQuery.PassageQuery : query;

        // Yield the stream directly from the LLM execution method
        var responseStream = StreamFromLLMAsync(
            chatClient,
            synthesisQuery,
            formattedContext,
            cancellationToken
        );

        return new StreamingSearchContextResult(
            topChunkIds,
            formattedContext,
            responseStream,
            userQuery
        );
    }

    private async IAsyncEnumerable<string> StreamFromLLMAsync(
        IChatClient chatClient,
        string userQuery,
        string context,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        using (chatClient)
        {
            IAsyncEnumerable<ChatResponseUpdate>? stream = null;

            try
            {
                var messages = new List<Microsoft.Extensions.AI.ChatMessage>
                {
                    new(ChatRole.System, Prompts.AskToLLMSystemPrompt),
                    new(ChatRole.User, $"User Query: {userQuery}\n\n{context}"),
                };

                stream = chatClient.GetStreamingResponseAsync(
                    messages,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize LLM streaming response.");
                yield break;
            }

            // Enumerate and yield tokens as they arrive from Microsoft.Extensions.AI
            await foreach (var update in stream.WithCancellation(cancellationToken))
            {
                if (!string.IsNullOrEmpty(update.Text))
                {
                    yield return update.Text;
                }
            }
        }
    }

    private async Task<List<int>> SearchAndMergeAsync(
        RefinedQuery query,
        CancellationToken cancellationToken
    )
    {
        string formattedFts = FormatFtsQuery(query.FTSKeywords);
        string cleanedPassage = CleanForVectorEmbedder(query.PassageQuery);
        var embeddingTask = Task.Run(
            () => JsonSerializer.Serialize(embedder.Embed(cleanedPassage).Values.ToArray()),
            cancellationToken
        );

        await using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(
            cancellationToken
        );

        Task<List<int>> ftsTask = !string.IsNullOrWhiteSpace(formattedFts)
            ? dbContext
                .Database.SqlQuery<int>(
                    $"SELECT rowid FROM file_chunks_fts WHERE file_chunks_fts MATCH {formattedFts} ORDER BY rank LIMIT 10"
                )
                .ToListAsync(cancellationToken)
            : Task.FromResult(new List<int>());

        string passageVectorJson = await embeddingTask;
        var chunkVectorTask = dbContext
            .Database.SqlQuery<int>(
                $"SELECT chunk_id FROM vec_file_chunks WHERE embedding MATCH {passageVectorJson} AND k = 10 ORDER BY distance ASC"
            )
            .ToListAsync(cancellationToken);

        await Task.WhenAll(ftsTask, chunkVectorTask);

        const int k = 60;
        var rrfScores = new Dictionary<int, double>();

        void ProcessStream(List<int> stream)
        {
            for (int rank = 0; rank < stream.Count; rank++)
            {
                int chunkId = stream[rank];
                double score = 1.0 / (k + rank + 1);

                if (!rrfScores.TryAdd(chunkId, score))
                {
                    rrfScores[chunkId] += score;
                }
            }
        }

        ProcessStream(ftsTask.Result);
        ProcessStream(chunkVectorTask.Result);

        return rrfScores
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .Take(10)
            .ToList();
    }

    private async Task<string> GetFormattedContextAsync(
        List<int> chunkIds,
        CancellationToken cancellationToken
    )
    {
        if (chunkIds == null || chunkIds.Count == 0)
        {
            return "No matching file excerpts were found.";
        }

        await using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(
            cancellationToken
        );

        var rawChunks = await dbContext
            .FileChunks.AsNoTracking()
            .Where(c => chunkIds.Contains(c.Id))
            .Select(c => new
            {
                c.Id,
                c.ChunkText,
                FilePath = c.FileEntry != null ? c.FileEntry.Path : "Unknown",
                CategoryName = c.FileEntry != null && c.FileEntry.PrimaryCategory != null
                    ? c.FileEntry.PrimaryCategory.Name
                    : null,
                DocTypeName = c.FileEntry != null && c.FileEntry.DocumentType != null
                    ? c.FileEntry.DocumentType.Name
                    : null,
            })
            .ToListAsync(cancellationToken);

        var chunkMap = rawChunks.ToDictionary(c => c.Id);

        var sb = new StringBuilder();
        sb.AppendLine("Context Excerpts:");
        sb.AppendLine("=================");

        int excerptIndex = 1;
        foreach (var id in chunkIds)
        {
            if (!chunkMap.TryGetValue(id, out var chunk))
                continue;

            sb.AppendLine($"[Excerpt {excerptIndex++}]");
            sb.AppendLine($"Source File: {chunk.FilePath}");

            if (chunk.CategoryName != null)
                sb.AppendLine($"Category: {chunk.CategoryName}");

            if (chunk.DocTypeName != null)
                sb.AppendLine($"Document Type: {chunk.DocTypeName}");

            sb.AppendLine("Content:");
            sb.AppendLine(chunk.ChunkText);
            sb.AppendLine("--------------------------------------------------");
        }

        return sb.ToString();
    }

    private async Task<RefinedQuery?> GetRefinedQueryAsync(
        IChatClient chatClient,
        string userPrompt,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, Prompts.RefineQuerySystemPrompt),
                new(ChatRole.User, $"User query: {userPrompt}"),
            };

            var completion = await chatClient.GetResponseAsync(
                messages,
                ChatOptions,
                cancellationToken
            );

            var cleanText = StringHelpers.CleanLLMJsonOutput(completion.Text);
            return JsonSerializer.Deserialize<RefinedQuery>(cleanText, JsonHelpers.JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to refine query using LLM. Falling back to raw query.");
            return null;
        }
    }

    private static RefinedQuery ValidateAndSanitizeRefinedQuery(
        RefinedQuery? query,
        string fallbackRawQuery
    )
    {
        if (query == null)
        {
            return new RefinedQuery(fallbackRawQuery, fallbackRawQuery, fallbackRawQuery);
        }

        static string CleanValue(string? input, string fallback)
        {
            if (string.IsNullOrWhiteSpace(input))
                return fallback;

            var trimmed = input.Replace("\u00A0", " ").Trim();

            if (
                trimmed.EndsWith("...")
                || trimmed.Length < 4
                || trimmed.Equals("We", StringComparison.OrdinalIgnoreCase)
            )
            {
                return fallback;
            }

            return trimmed.TrimEnd('.');
        }

        var fts = CleanValue(query.FTSKeywords, fallbackRawQuery);
        var metadata = CleanValue(query.MetadataQuery, fallbackRawQuery);
        var passage = CleanValue(query.PassageQuery, fallbackRawQuery);

        return new RefinedQuery(fts, metadata, passage);
    }

    private static string FormatFtsQuery(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var words = Regex
            .Matches(input, @"\w+")
            .Select(m => m.Value)
            .Where(w => w.Length > 1)
            .Distinct();

        if (!words.Any())
            return string.Empty;

        return string.Join(" OR ", words);
    }

    private static string CleanForVectorEmbedder(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return input
            .Replace("\"", "")
            .Replace("'", "")
            .Replace("intitle:", "", StringComparison.OrdinalIgnoreCase)
            .Replace("body:", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
    }
}
