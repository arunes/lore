using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Lore.Core.RAG;

public interface IActiveChatCache
{
    ChatHistory? GetAgenticHistory();

    void SetAgenticHistory(ChatHistory history);

    List<ChatMessage>? GetTraditionalHistory();

    void SetTraditionalHistory(List<ChatMessage> history);

    void Clear();
}

public sealed class ActiveChatCache(IMemoryCache memoryCache) : IActiveChatCache
{
    private const string AgenticKey = "chat-active-agentic";
    private const string TraditionalKey = "chat-active-traditional";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public ChatHistory? GetAgenticHistory() =>
        memoryCache.TryGetValue(AgenticKey, out ChatHistory? history) ? history : null;

    public void SetAgenticHistory(ChatHistory history) =>
        memoryCache.Set(AgenticKey, history, CacheDuration);

    public List<ChatMessage>? GetTraditionalHistory() =>
        memoryCache.TryGetValue(TraditionalKey, out List<ChatMessage>? history) ? history : null;

    public void SetTraditionalHistory(List<ChatMessage> history) =>
        memoryCache.Set(TraditionalKey, history, CacheDuration);

    public void Clear()
    {
        memoryCache.Remove(AgenticKey);
        memoryCache.Remove(TraditionalKey);
    }
}
