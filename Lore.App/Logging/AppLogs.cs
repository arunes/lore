using Microsoft.Extensions.Logging;

namespace Lore.App.Logging;

internal static partial class AppLogs
{
    [LoggerMessage(EventId = 1501, Level = LogLevel.Debug, Message = "Chat request received, chatId {chatId}, prompt {promptChars} chars")]
    public static partial void ChatRequestReceived(this ILogger logger, string chatId, int promptChars);
}