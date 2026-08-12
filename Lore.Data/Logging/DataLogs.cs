using Microsoft.Extensions.Logging;

namespace Lore.Data.Logging;

internal static partial class DataLogs
{
    [LoggerMessage(EventId = 1401, Level = LogLevel.Information, Message = "Initializing SQLite database and vector extensions")]
    public static partial void DbInitializing(this ILogger logger);

    [LoggerMessage(EventId = 1402, Level = LogLevel.Information, Message = "Database initialization completed")]
    public static partial void DbInitialized(this ILogger logger);

    [LoggerMessage(EventId = 1403, Level = LogLevel.Debug, Message = "Migrations applied")]
    public static partial void MigrationsApplied(this ILogger logger);

    [LoggerMessage(EventId = 1404, Level = LogLevel.Debug, Message = "Vector tables ensured")]
    public static partial void VectorTablesEnsured(this ILogger logger);

    [LoggerMessage(EventId = 1405, Level = LogLevel.Debug, Message = "FTS tables ensured")]
    public static partial void FtsTablesEnsured(this ILogger logger);
}