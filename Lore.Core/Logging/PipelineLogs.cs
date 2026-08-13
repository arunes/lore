using Microsoft.Extensions.Logging;

namespace Lore.Core.Logging;

internal static partial class PipelineLogs
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Startup pipeline initialized")]
    public static partial void StartupComplete(this ILogger logger);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Resuming {count} files with statuses: {statuses}")]
    public static partial void ResumeStarted(this ILogger logger, int count, string statuses);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Debug, Message = "Resumed file {id} at status {status} → next stage {nextStage}")]
    public static partial void ResumedFile(this ILogger logger, int id, string status, string nextStage);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Watching directory {directory} (excluding {excludeExtensions})")]
    public static partial void WatchDirectoryStarted(this ILogger logger, string directory, string excludeExtensions);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Warning, Message = "Directory {directory} does not exist, skipping")]
    public static partial void DirectoryMissing(this ILogger logger, string directory);

    [LoggerMessage(EventId = 1010, Level = LogLevel.Debug, Message = "Drained {count} {requestType} in {elapsedMs}ms")]
    public static partial void BatchDrained(this ILogger logger, int count, string requestType, long elapsedMs);

    [LoggerMessage(EventId = 1011, Level = LogLevel.Information, Message = "FileArrival starting for {count} files")]
    public static partial void FileArrivalStarted(this ILogger logger, int count);

    [LoggerMessage(EventId = 1012, Level = LogLevel.Debug, Message = "File unchanged, skipping {path}")]
    public static partial void FileUnchanged(this ILogger logger, string path);

    [LoggerMessage(EventId = 1013, Level = LogLevel.Debug, Message = "File {path} hashed ({len} chars)")]
    public static partial void FileHashed(this ILogger logger, string path, int len);

    [LoggerMessage(EventId = 1014, Level = LogLevel.Warning, Message = "File {path} not found in database, skipping")]
    public static partial void FileMissingInDb(this ILogger logger, string path);

    [LoggerMessage(EventId = 1015, Level = LogLevel.Debug, Message = "Enqueued {count} {nextStage} requests")]
    public static partial void StageHandoff(this ILogger logger, int count, string nextStage);

    [LoggerMessage(EventId = 1016, Level = LogLevel.Information, Message = "FileArrival finished: {newCount} new, {unchangedCount} unchanged, {deletedCount} deleted, {skippedCount} skipped")]
    public static partial void FileArrivalFinished(this ILogger logger, int newCount, int unchangedCount, int deletedCount, int skippedCount);

    [LoggerMessage(EventId = 1020, Level = LogLevel.Information, Message = "TextExtract starting for {count} files")]
    public static partial void TextExtractStarted(this ILogger logger, int count);

    [LoggerMessage(EventId = 1021, Level = LogLevel.Debug, Message = "Extracted {path} via {extractor} → {chars} chars")]
    public static partial void ExtractionOutcome(this ILogger logger, string path, string extractor, int chars);

    [LoggerMessage(EventId = 1022, Level = LogLevel.Debug, Message = "No extractable text for {path}")]
    public static partial void ExtractionEmpty(this ILogger logger, string path);

    [LoggerMessage(EventId = 1023, Level = LogLevel.Warning, Message = "Unsupported file {path} ({extension})")]
    public static partial void FileNotSupported(this ILogger logger, string path, string extension);

    [LoggerMessage(EventId = 1024, Level = LogLevel.Information, Message = "TextExtract finished: {extracted} extracted, {empty} empty, {notSupported} unsupported, {failed} failed")]
    public static partial void TextExtractFinished(this ILogger logger, int extracted, int empty, int notSupported, int failed);

    [LoggerMessage(EventId = 1030, Level = LogLevel.Information, Message = "Classify starting for {count} files")]
    public static partial void ClassifyStarted(this ILogger logger, int count);

    [LoggerMessage(EventId = 1031, Level = LogLevel.Debug, Message = "Classified file {id}: {path} → {category} / {docType}")]
    public static partial void FileClassified(this ILogger logger, int id, string path, string category, string docType);

    [LoggerMessage(EventId = 1032, Level = LogLevel.Warning, Message = "No category or document type matched for file {id}")]
    public static partial void NoCategoryMatched(this ILogger logger, int id);

    [LoggerMessage(EventId = 1033, Level = LogLevel.Information, Message = "Classify finished: {classified} classified, {failed} failed")]
    public static partial void ClassifyFinished(this ILogger logger, int classified, int failed);

    [LoggerMessage(EventId = 1040, Level = LogLevel.Information, Message = "Chunking starting for {count} files")]
    public static partial void ChunkingStarted(this ILogger logger, int count);

    [LoggerMessage(EventId = 1041, Level = LogLevel.Debug, Message = "Chunked file {id}: {chunkCount} chunks, {chars} chars")]
    public static partial void FileChunked(this ILogger logger, int id, int chunkCount, int chars);

    [LoggerMessage(EventId = 1042, Level = LogLevel.Information, Message = "Chunking finished: {files} files, {totalChunks} total chunks")]
    public static partial void ChunkingFinished(this ILogger logger, int files, int totalChunks);

    [LoggerMessage(EventId = 1050, Level = LogLevel.Information, Message = "Vectorize starting for {count} files")]
    public static partial void VectorizeStarted(this ILogger logger, int count);

    [LoggerMessage(EventId = 1051, Level = LogLevel.Debug, Message = "Vectorized file {id}: {chunkCount} embeddings in {elapsedMs}ms")]
    public static partial void FileVectorized(this ILogger logger, int id, int chunkCount, long elapsedMs);

    [LoggerMessage(EventId = 1052, Level = LogLevel.Warning, Message = "File {id} produced no vectors")]
    public static partial void ZeroVectorsWritten(this ILogger logger, int id);

    [LoggerMessage(EventId = 1053, Level = LogLevel.Information, Message = "Vectorize finished: {files} files, {vectors} total vectors")]
    public static partial void VectorizeFinished(this ILogger logger, int files, int vectors);

    [LoggerMessage(EventId = 1060, Level = LogLevel.Debug, Message = "Watcher: skipping {path} (excluded)")]
    public static partial void WatcherIgnored(this ILogger logger, string path);

    [LoggerMessage(EventId = 1061, Level = LogLevel.Error, Message = "Error occurred on file watcher")]
    public static partial void WatcherError(this ILogger logger, Exception ex);

    [LoggerMessage(EventId = 1070, Level = LogLevel.Debug, Message = "Loaded {count} settings from database")]
    public static partial void SettingsLoaded(this ILogger logger, int count);

    [LoggerMessage(EventId = 1071, Level = LogLevel.Error, Message = "Failed to classify file {id}")]
    public static partial void ClassifyFailed(this ILogger logger, int id, Exception ex);

    [LoggerMessage(EventId = 1072, Level = LogLevel.Error, Message = "Failed to extract text for {path}")]
    public static partial void ExtractionFailed(this ILogger logger, string path, Exception ex);

    [LoggerMessage(EventId = 1073, Level = LogLevel.Error, Message = "Failed to write vectors for file {id}")]
    public static partial void VectorWriteFailed(this ILogger logger, int id, Exception ex);

    [LoggerMessage(EventId = 1074, Level = LogLevel.Warning, Message = "File ID {id} not found, skipping chunking")]
    public static partial void ChunkingFileMissing(this ILogger logger, int id);

    [LoggerMessage(EventId = 1075, Level = LogLevel.Warning, Message = "File ID {id} not found, skipping classification")]
    public static partial void ClassifyFileMissing(this ILogger logger, int id);

    [LoggerMessage(EventId = 1076, Level = LogLevel.Debug, Message = "File {path} does not exist on disk, removing from index")]
    public static partial void FileDeleted(this ILogger logger, string path);
}