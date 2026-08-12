using Lore.Common.Models;

namespace Lore.Core.RAG;

public interface IRAGService
{
    Task<LoreChatResponse> ChatAsync(
        LoreChatRequest request,
        CancellationToken cancellationToken = default
    );
}