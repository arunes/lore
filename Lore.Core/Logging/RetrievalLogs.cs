using Microsoft.Extensions.Logging;

namespace Lore.Core.Logging;

internal static partial class RetrievalLogs
{
    [LoggerMessage(EventId = 1201, Level = LogLevel.Debug, Message = "Retrieval: FTS {ftsCount} + vector {vectorCount} → fused {fusedCount} results in {elapsedMs}ms")]
    public static partial void RetrievalResult(this ILogger logger, int ftsCount, int vectorCount, int fusedCount, long elapsedMs);

    [LoggerMessage(EventId = 1202, Level = LogLevel.Debug, Message = "Embedding cache loaded: {categories} categories, {documentTypes} document types")]
    public static partial void EmbeddingCacheLoaded(this ILogger logger, int categories, int documentTypes);
}