using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Lore.Common.Models;
using Lore.Core.Logging;
using Lore.Core.Settings;
using Lore.Core.Telemetry;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Lore.Core.RAG;

public class AgenticRAGService(
    ILogger<AgenticRAGService> logger,
    IUserSettingsService userSettings,
    IMemoryCache memoryCache,
    IKernelFactory kernelFactory
) : IRAGService
{
    private static string GetChatCacheKey(Guid chatId) => $"chat-agent-{chatId}";

    public async Task<LoreChatResponse> ChatAsync(LoreChatRequest request, CancellationToken cancellationToken = default)
    {
        var chatId = request.ChatId ?? Guid.NewGuid();
        var chatSid = chatId.ToString("N")[..8];

        using var activity = LoreActivitySource.Source.StartActivity("chat/agentic");
        activity?.SetTag("chat.id", chatSid);

        logger.ChatStarted(chatSid, "Agentic");

        memoryCache.TryGetValue(GetChatCacheKey(chatId), out ChatHistory? conversationHistory);
        conversationHistory ??= new ChatHistory(
            userSettings.GetSetting<string>(UserSettingsType.AgenticSystemPrompt),
            AuthorRole.System);

        var kernel = kernelFactory.CreateKernel();
        var responseStream = StreamFromLLMAsync(
            kernel,
            conversationHistory,
            chatId,
            chatSid,
            request.Prompt,
            cancellationToken
        );

        return new LoreChatResponse(chatId, responseStream);
    }

    private async IAsyncEnumerable<string> StreamFromLLMAsync(
        Kernel kernel,
        ChatHistory messageHistory,
        Guid chatId,
        string chatSid,
        string userMessage,
        [EnumeratorCancellation] CancellationToken cancellationToken = default
    )
    {
        using var activity = LoreActivitySource.Source.StartActivity("chat/agentic/llm_stream");

        var sw = Stopwatch.StartNew();
        var currentTurnMessages = new ChatHistory(messageHistory);
        currentTurnMessages.AddUserMessage(userMessage);

        IAsyncEnumerable<StreamingChatMessageContent> responseStream;
        try
        {
            var chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                Temperature = userSettings.GetSetting<float>(UserSettingsType.ChatTemperature)
            };

            responseStream = chatCompletion.GetStreamingChatMessageContentsAsync(
                currentTurnMessages,
                executionSettings,
                kernel,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            logger.StreamInitFailed(chatSid, ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            yield break;
        }

        var llmResponse = new StringBuilder();
        await foreach (var update in responseStream.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrWhiteSpace(update.Content))
            {
                llmResponse.Append(update.Content);
                yield return update.Content;
            }
        }

        sw.Stop();
        logger.StreamedSummary(chatSid, llmResponse.Length, sw.ElapsedMilliseconds);

        activity?.SetTag("chat.stream_chars", llmResponse.Length);
        activity?.SetTag("chat.stream_duration_ms", sw.ElapsedMilliseconds);

        LoreMetrics.RagChats.Add(1,
            new KeyValuePair<string, object?>("backend", "agentic"),
            new KeyValuePair<string, object?>("result", "success"));
        LoreMetrics.RagLlmStreamDuration.Record(sw.ElapsedMilliseconds,
            new KeyValuePair<string, object?>("backend", "agentic"));
        LoreMetrics.RagLlmStreamChars.Record(llmResponse.Length,
            new KeyValuePair<string, object?>("backend", "agentic"));

        currentTurnMessages.AddAssistantMessage(llmResponse.ToString());
        memoryCache.Set(GetChatCacheKey(chatId), currentTurnMessages, TimeSpan.FromMinutes(15));
    }
}