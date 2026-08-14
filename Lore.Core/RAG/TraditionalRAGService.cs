using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Lore.Common.Models;
using Microsoft.Extensions.Caching.Memory;
using Lore.Common.Extensions;
using Lore.Core.Logging;
using Lore.Core.Retrieval;
using Lore.Core.Settings;
using Lore.Core.Telemetry;

using System.ClientModel;

namespace Lore.Core.RAG;

public class TraditionalRAGService(
    ILogger<TraditionalRAGService> logger,
    IUserSettingsService userSettings,
    IRetrievalService searchTools,
    IMemoryCache memoryCache
) : IRAGService
{
    private static string GetChatCacheKey(Guid chatId) => $"chat-trad-{chatId}";

    public async Task<LoreChatResponse> ChatAsync(
        LoreChatRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var chatId = request.ChatId ?? Guid.NewGuid();
        var chatSid = chatId.ToString("N")[..8];

        using var activity = LoreActivitySource.Source.StartActivity("chat/traditional");
        activity?.SetTag("chat.id", chatSid);

        logger.ChatStarted(chatSid, "Traditional");

        var chatClient = CreateChatClient();

        memoryCache.TryGetValue(GetChatCacheKey(chatId), out List<ChatMessage>? conversationHistory);
            conversationHistory ??= [new(ChatRole.System, userSettings.GetSetting<string>(UserSettingsType.TraditionalSystemPrompt))];

        var query = await GetRetrievalQueryAsync(chatClient, conversationHistory, request, chatSid, cancellationToken);
        var documentChunkIds = query.NeedsRetrieval ? await searchTools.RetrieveDocumentChunksAsync(query, cancellationToken) : [];

        if (query.NeedsRetrieval && documentChunkIds.Count == 0)
        {
            logger.NoRelevantChunks(chatSid);
        }

        var userMessage = await BuildCurrentUserMessageAsync(request.Prompt, query.NeedsRetrieval, documentChunkIds, cancellationToken);

        var responseStream = StreamFromLLMAsync(
            chatClient,
            conversationHistory,
            chatId,
            chatSid,
            userMessage,
            cancellationToken
        );

        return new LoreChatResponse(chatId, responseStream);
    }

    private async Task<string> BuildCurrentUserMessageAsync(
            string prompt,
            bool retrievedFromDb,
            List<int> documentChunkIds,
            CancellationToken cancellationToken)
    {
        string GetMessage(string context)
        {
            return $"""
    <retrieved_context>
    {context}
    </retrieved_context>

    <current_question>
    {prompt}
    </current_question>
    """;
        }

        if (documentChunkIds.Count == 0)
        {
            return retrievedFromDb
                ? GetMessage("No relevant document context was retrieved.")
                : prompt;
        }

        var chunksByFile = await searchTools.GetChunkContentsAsync(documentChunkIds, cancellationToken);

        var sb = new StringBuilder();
        foreach (var file in chunksByFile)
        {
            sb.AppendLine("<file>");
            sb.AppendLine($"id: {file.Id}");
            sb.AppendLine($"path: {file.Path}");
            sb.AppendLine($"category: {file.CategoryName ?? "Undefined"}");
            sb.AppendLine($"documentType: {file.DocTypeName ?? "Undefined"}");

            foreach (var chunk in file.Chunks.OrderBy(c => c.ChunkIndex))
            {
                sb.AppendLine($"<chunk index=\"{chunk.ChunkIndex}\">");
                sb.AppendLine(chunk.ChunkText);
                sb.AppendLine("</chunk>");
            }

            sb.AppendLine("</file>");
        }

        return GetMessage(sb.ToString());
    }

    private async IAsyncEnumerable<string> StreamFromLLMAsync(
        IChatClient chatClient,
        List<ChatMessage> messageHistory,
        Guid chatId,
        string chatSid,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        using var activity = LoreActivitySource.Source.StartActivity("chat/traditional/llm_stream");

        var sw = Stopwatch.StartNew();
        var currentTurnMessages = new List<ChatMessage>(messageHistory)
        {
            new(ChatRole.User, userMessage)
        };

        IAsyncEnumerable<ChatResponseUpdate>? stream;
        try
        {
            var chatOptions = new ChatOptions
            {
                Temperature = userSettings.GetSetting<float?>(UserSettingsType.ChatTemperature),
                Reasoning = new ReasoningOptions
                {
                    Effort = ReasoningEffort.None
                }
            };

            stream = chatClient.GetStreamingResponseAsync(
                currentTurnMessages,
                chatOptions,
                cancellationToken: cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.StreamInitFailed(chatSid, ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
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

        sw.Stop();
        logger.StreamedSummary(chatSid, llmResponse.Length, sw.ElapsedMilliseconds);

        activity?.SetTag("chat.stream_chars", llmResponse.Length);
        activity?.SetTag("chat.stream_duration_ms", sw.ElapsedMilliseconds);

        LoreMetrics.RagChats.Add(1,
            new KeyValuePair<string, object?>("backend", "traditional"),
            new KeyValuePair<string, object?>("result", "success"));
        LoreMetrics.RagLlmStreamDuration.Record(sw.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("backend", "traditional"));
        LoreMetrics.RagLlmStreamChars.Record(llmResponse.Length,
            new KeyValuePair<string, object?>("backend", "traditional"));

        currentTurnMessages.Add(new ChatMessage(ChatRole.Assistant, llmResponse.ToString()));
        memoryCache.Set(GetChatCacheKey(chatId), currentTurnMessages, TimeSpan.FromMinutes(15));
    }

    private async Task<RetrievalQuery> GetRetrievalQueryAsync(
        IChatClient chatClient,
        List<ChatMessage> conversationHistory,
        LoreChatRequest request,
        string chatSid,
        CancellationToken cancellationToken)
    {
        using var activity = LoreActivitySource.Source.StartActivity("chat/traditional/retrieval_query");

        var defaultQuery = new RetrievalQuery
        {
            NeedsRetrieval = true,
            FTSTerms = [.. request.Prompt.Split(' ')],
            SearchQuery = request.Prompt
        };

        try
        {
            var systemPrompt = userSettings.GetSetting<string>(UserSettingsType.RetrievalQueryPrompt);

            var history = conversationHistory
                .Where(h => h.Role != ChatRole.System)
                .OrderBy(h => h.CreatedAt)
                .Select(h => $"{h.Role}:\n{h.Text}")
                .DefaultIfEmpty("History is not available.");

            var prompt = $"""
            CONVERSATION HISTORY:
            {string.Join("\n\n", history)}

            LATEST USER MESSAGE:
            {request.Prompt}
            """;

            var chatOptions = new ChatOptions
            {
                ResponseFormat = ChatResponseFormat.ForJsonSchema(
                        schema: AIJsonUtilities.CreateJsonSchema(typeof(RetrievalQuery)),
                        schemaName: "retrieval_query"
                    ),
                Temperature = userSettings.GetSetting<float?>(UserSettingsType.RetrievalQueryTemperature),
                Reasoning = new ReasoningOptions
                {
                    Effort = ReasoningEffort.None
                }
            };

            var completion = await chatClient.GetResponseAsync(
                 [
                    new(ChatRole.System, systemPrompt),
                    new(ChatRole.User, prompt)
                ],
                chatOptions,
                cancellationToken
            );

            var cleanText = completion.Text.CleanLLMJsonOutput();
            var response = cleanText.DeserializeJson<RetrievalQuery>();
            var finalQuery = response ?? defaultQuery;
            logger.RetrievalDecision(chatSid, finalQuery.NeedsRetrieval, finalQuery.SearchQuery?.Length ?? 0, finalQuery.FTSTerms.Count);

            activity?.SetTag("chat.needs_retrieval", finalQuery.NeedsRetrieval);
            return finalQuery;
        }
        catch (Exception ex)
        {
            logger.RetrievalQueryFallback(chatSid, ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            return defaultQuery;
        }
    }

    private IChatClient CreateChatClient()
    {
        var aiEndpoint = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIUrl);
        var aiAuthKey = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIKey);
        var aiModel = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIModel);

        var clientOptions = new OpenAI.OpenAIClientOptions
        {
            Endpoint = new Uri(aiEndpoint)
        };

        return new OpenAI.Chat.ChatClient(aiModel, new ApiKeyCredential(aiAuthKey), clientOptions).AsIChatClient();
    }
}