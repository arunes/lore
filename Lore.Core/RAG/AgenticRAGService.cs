using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Lore.Common.Models;
using Lore.Core.Logging;
using Lore.Core.Settings;
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

        logger.ChatStarted(chatSid, "Agentic");

        memoryCache.TryGetValue(GetChatCacheKey(chatId), out ChatHistory? conversationHistory);
        conversationHistory ??= new ChatHistory(
            userSettings.GetSetting<string>(UserSettingsType.LoreChatAgenticSystemPrompt),
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
                Temperature = userSettings.GetSetting<float>(UserSettingsType.SearchChatTemperature)
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
            yield break;
        }

        var llmResponse = new StringBuilder();
        await foreach (var update in responseStream.WithCancellation(cancellationToken))
        {
            if (!string.IsNullOrEmpty(update.Content))
            {
                llmResponse.Append(update.Content);
                yield return update.Content;
            }
        }

        sw.Stop();
        logger.StreamedSummary(chatSid, llmResponse.Length, sw.ElapsedMilliseconds);

        currentTurnMessages.AddAssistantMessage(llmResponse.ToString());
        memoryCache.Set(GetChatCacheKey(chatId), currentTurnMessages, TimeSpan.FromMinutes(15));
    }
}