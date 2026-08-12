using Microsoft.Extensions.Logging;

namespace Lore.Core.Logging;

internal static partial class RagLogs
{
    [LoggerMessage(EventId = 1301, Level = LogLevel.Debug, Message = "Chat {chatId} started via {backend}")]
    public static partial void ChatStarted(this ILogger logger, string chatId, string backend);

    [LoggerMessage(EventId = 1302, Level = LogLevel.Debug, Message = "Chat {chatId}: needsRetrieval={needsRetrieval}, searchQueryLen={searchQueryLen}, ftsTerms={ftsTermCount}")]
    public static partial void RetrievalDecision(this ILogger logger, string chatId, bool needsRetrieval, int searchQueryLen, int ftsTermCount);

    [LoggerMessage(EventId = 1303, Level = LogLevel.Warning, Message = "Chat {chatId}: no relevant chunks retrieved")]
    public static partial void NoRelevantChunks(this ILogger logger, string chatId);

    [LoggerMessage(EventId = 1304, Level = LogLevel.Debug, Message = "Chat {chatId}: streamed {chars} chars in {elapsedMs}ms")]
    public static partial void StreamedSummary(this ILogger logger, string chatId, int chars, long elapsedMs);

    [LoggerMessage(EventId = 1305, Level = LogLevel.Error, Message = "Chat {chatId}: failed to initialize LLM streaming response")]
    public static partial void StreamInitFailed(this ILogger logger, string chatId, Exception ex);

    [LoggerMessage(EventId = 1306, Level = LogLevel.Warning, Message = "Chat {chatId}: retrieval query failed, falling back to raw query")]
    public static partial void RetrievalQueryFallback(this ILogger logger, string chatId, Exception ex);

    [LoggerMessage(EventId = 1307, Level = LogLevel.Debug, Message = "Created kernel: model {model}, endpoint {endpoint}, {pluginCount} plugins loaded")]
    public static partial void KernelCreated(this ILogger logger, string model, string endpoint, int pluginCount);
}