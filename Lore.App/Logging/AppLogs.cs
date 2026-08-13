using Microsoft.Extensions.Logging;

namespace Lore.App.Logging;

internal static partial class AppLogs
{
    [LoggerMessage(EventId = 1501, Level = LogLevel.Debug, Message = "Chat request received, chatId {chatId}, prompt {promptChars} chars")]
    public static partial void ChatRequestReceived(this ILogger logger, string chatId, int promptChars);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Error, Message = "Unhandled exception, traceId {traceId}")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string traceId);
}
