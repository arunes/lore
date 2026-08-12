using Microsoft.Extensions.Logging;

namespace Lore.Core.Logging;

internal static partial class ExtractorLogs
{
    [LoggerMessage(EventId = 1101, Level = LogLevel.Warning, Message = "Failed to extract text from {path} as {format}")]
    public static partial void ExtractionWarning(this ILogger logger, string path, string format, Exception ex);

    [LoggerMessage(EventId = 1102, Level = LogLevel.Debug, Message = "Unreadable {format} content for {path}")]
    public static partial void UnreadableFormat(this ILogger logger, string format, string path);
}