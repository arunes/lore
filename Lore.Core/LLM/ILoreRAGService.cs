using Lore.Common.Models;

namespace Lore.Core.LLM;

public interface ILoreRAGService
{
    Task<LoreChatResponse> ChatAsync(
        LoreChatRequest request,
        CancellationToken cancellationToken = default
    );
}