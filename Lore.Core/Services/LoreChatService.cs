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
using Microsoft.Extensions.Caching.Memory;

namespace Lore.Core.Services;

public record StreamingSearchContextResult(
    Guid ChatId,
    string FormattedContext,
    IAsyncEnumerable<string> LLMResponseStream,
    RefinedQuery Query
);

public interface ILoreChatService
{
    Task<StreamingSearchContextResult> ChatAsync(
        LoreChatRequest request,
        CancellationToken cancellationToken = default
    );
}

public class LoreChatService(
    ILogger<LoreChatService> logger,
    IUserSettingsService userSettings,
    LocalEmbedder embedder,
    IDbContextFactory<LoreDbContext> dbContextFactory,
    IChatClientFactory chatClientFactory,
    IMemoryCache memoryCache
) : ILoreChatService
{
    private static string GetChatCacheKey(Guid chatId) => $"chat-{chatId}";

    public async Task<StreamingSearchContextResult> ChatAsync(
        LoreChatRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var chatId = request.ChatId ?? Guid.NewGuid();
        var chatClient = await chatClientFactory.CreateClientAsync(cancellationToken);

        memoryCache.TryGetValue(GetChatCacheKey(chatId), out List<ChatMessage>? messageHistory);
        messageHistory ??= [new(ChatRole.System, userSettings.GetSetting<string>(UserSettingsType.LoreChatSystemPrompt))];

        var userPrompt = await GetUserPromptAsync(chatClient, request, messageHistory, cancellationToken);
        string ragContext;

        if (IsNoSearchRequired(userPrompt))
        {
            logger.LogInformation("No search required for chat ID {ChatId}.", chatId);
            ragContext = "No external file search was needed for this turn.";
        }
        else
        {
            var documentChunkIds = await SearchDocuments(userPrompt, cancellationToken);
            ragContext = await GetRagContext(documentChunkIds, cancellationToken);
        }

        var responseStream = StreamFromLLMAsync(
            chatClient,
            messageHistory,
            chatId,
            request.Prompt,
            ragContext,
            cancellationToken
        );

        return new StreamingSearchContextResult(
            chatId,
            ragContext,
            responseStream,
            userPrompt
        );
    }

    private async Task<RefinedQuery> GetUserPromptAsync(
        IChatClient chatClient,
        LoreChatRequest request,
        List<ChatMessage> messageHistory,
        CancellationToken cancellationToken
    )
    {
        var sanitized = request.Prompt.Replace("'", "''").Replace("\"", "").Trim();

        RefinedQuery? userPrompt = null;
        if (request.RefinePrompt)
        {
            userPrompt = await GetRefinedQueryAsync(chatClient, request.Prompt, messageHistory, cancellationToken);
        }

        return ValidateAndSanitizeRefinedQuery(userPrompt, sanitized);
    }
    private async Task<RefinedQuery?> GetRefinedQueryAsync(
        IChatClient chatClient,
        string userPrompt,
        List<ChatMessage> messageHistory,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var rewriterMessages = new List<ChatMessage>
            {
                new(ChatRole.System, userSettings.GetSetting<string>(UserSettingsType.RefineQuerySystemPrompt))
            };

            foreach (var historyMsg in messageHistory.Where(m => m.Role != ChatRole.System))
            {
                rewriterMessages.Add(historyMsg);
            }

            rewriterMessages.Add(new ChatMessage(ChatRole.User, $"User query: {userPrompt}"));

            var chatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(RefinedQuery)),
                        schemaName: "document_metadata_schema"
                    ),
                Temperature = userSettings.GetSetting<float>(UserSettingsType.SearchRefinmentTemperature),
            };

            var completion = await chatClient.GetResponseAsync(
                rewriterMessages,
                chatOptions,
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

    private async IAsyncEnumerable<string> StreamFromLLMAsync(
        IChatClient chatClient,
        List<ChatMessage> messageHistory,
        Guid chatId,
        string rawUserPrompt,
        string docContext,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        var currentTurnMessages = new List<ChatMessage>(messageHistory);

        string turnMessage = string.IsNullOrWhiteSpace(docContext)
            ? rawUserPrompt
            : $"User Query: {rawUserPrompt}\n\n{docContext}";

        currentTurnMessages.Add(new ChatMessage(ChatRole.User, turnMessage));

        IAsyncEnumerable<ChatResponseUpdate>? stream = null;
        try
        {
            var chatOptions = new ChatOptions
            {
                Temperature = userSettings.GetSetting<float>(UserSettingsType.SearchChatTemperature),
            };
            
            stream = chatClient.GetStreamingResponseAsync(
                currentTurnMessages,
                chatOptions,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize LLM streaming response.");
            yield break;
        }

        var llmResponse = new StringBuilder();
        await foreach (var update in stream.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Text))
            {
                llmResponse.Append(update.Text);
                yield return update.Text;
            }
        }

        messageHistory.Add(new ChatMessage(ChatRole.User, rawUserPrompt));
        messageHistory.Add(new ChatMessage(ChatRole.Assistant, llmResponse.ToString()));
        memoryCache.Set(GetChatCacheKey(chatId), messageHistory, TimeSpan.FromMinutes(15));
    }

    private async Task<List<int>> SearchDocuments(
        RefinedQuery query,
        CancellationToken cancellationToken
    )
    {
        var formattedFts = FormatFtsQuery(query.FTSKeywords);
        var cleanedPassage = CleanForVectorEmbedder(query.PassageQuery);
        var maxNumberSearchResults = userSettings.GetSetting<int>(UserSettingsType.MaxNumberSearchResults);

        var embeddingTask = Task.Run(
            () => JsonSerializer.Serialize(embedder.Embed(cleanedPassage).Values.ToArray()),
            cancellationToken
        );

        await using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(cancellationToken);

        Task<List<int>> ftsTask = !string.IsNullOrWhiteSpace(formattedFts)
            ? dbContext
                .Database.SqlQuery<int>(
                    $"SELECT rowid FROM file_chunks_fts WHERE file_chunks_fts MATCH {formattedFts} ORDER BY rank LIMIT {maxNumberSearchResults}"
                )
                .ToListAsync(cancellationToken)
            : Task.FromResult(new List<int>());

        string passageVectorJson = await embeddingTask;
        var chunkVectorTask = dbContext
            .Database.SqlQuery<int>(
                $"SELECT chunk_id FROM vec_file_chunks WHERE embedding MATCH {passageVectorJson} AND k = {maxNumberSearchResults} ORDER BY distance ASC"
            )
            .ToListAsync(cancellationToken);

        await Task.WhenAll(ftsTask, chunkVectorTask);

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
        ProcessStream(ftsTask.Result, userSettings.GetSetting<float>(UserSettingsType.SearchFTSWeight));
        ProcessStream(chunkVectorTask.Result, userSettings.GetSetting<float>(UserSettingsType.SearchVectorWeight));

        return rrfScores
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => kvp.Key)
            .Take(maxNumberSearchResults)
            .ToList();
    }

    private async Task<string> GetRagContext(
        List<int> chunkIds,
        CancellationToken cancellationToken
    )
    {
        if (chunkIds == null || chunkIds.Count == 0)
        {
            return "No matching file excerpts were found.";
        }

        await using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(cancellationToken);

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

            if (trimmed.EndsWith("...") || trimmed.Length < 2)
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

    private static bool IsNoSearchRequired(RefinedQuery query)
    {
        return string.Equals(query.PassageQuery, "NO_SEARCH", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(query.FTSKeywords, "NO_SEARCH", StringComparison.OrdinalIgnoreCase);
    }
}