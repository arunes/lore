namespace Lore.Common.Models;

public record LoreChatResponse(Guid ChatId, IAsyncEnumerable<string> LLMResponseStream);