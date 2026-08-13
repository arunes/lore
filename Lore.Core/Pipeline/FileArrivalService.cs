using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using EFCore.BulkExtensions;
using Lore.Common.Extensions;
using Lore.Core.Logging;
using Lore.Core.Telemetry;
using Lore.Data;
using Lore.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lore.Core.Pipeline;

public class FileArrivalService(
    ILogger<FileArrivalService> logger,
    Channel<TextExtractRequest> textExtractChannel,
    IDbContextFactory<LoreDbContext> dbContextFactory
) : IChannelService<FileArrivalRequest>
{
    public int GetBatchSize() => 500;

    public async Task ProcessAsync(
        FileArrivalRequest request,
        CancellationToken cancellationToken
    ) => await ProcessBatchAsync([request], cancellationToken);

    public async Task ProcessBatchAsync(
        IReadOnlyList<FileArrivalRequest> requests,
        CancellationToken cancellationToken
    )
    {
        logger.FileArrivalStarted(requests.Count);

        var fileEntries = new ConcurrentBag<(FileEntry Entry, string? TraceParent)>();
        var filesToDelete = new ConcurrentBag<string>();
        var unchangedCount = 0;
        var skippedCount = 0;

        Dictionary<string, DateTime> existingFiles = [];
        var incomingPaths = requests.Select(req => req.FilePath).Distinct();
        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            existingFiles = await dbContext
                .Files.AsNoTracking()
                .Where(fl => incomingPaths.Contains(fl.Path))
                .Select(fl => new { fl.Path, fl.FileModifiedAt })
                .ToDictionaryAsync(
                    fl => fl.Path,
                    fl => fl.FileModifiedAt,
                    StringComparer.OrdinalIgnoreCase,
                    cancellationToken
                );
        }

        using var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        var tasks = requests.Select(async request =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                using var activity = TracingHelper.StartStageSpan("arrival", request.FilePath, request.TraceParent);

                var sw = Stopwatch.StartNew();
                string? result = null;

                var fileInfo = new FileInfo(request.FilePath);
                if (!fileInfo.Exists)
                {
                    if (existingFiles.ContainsKey(request.FilePath))
                    {
                        filesToDelete.Add(request.FilePath);
                        logger.FileDeleted(request.FilePath);
                        result = "deleted";
                    }
                    else
                    {
                        Interlocked.Increment(ref skippedCount);
                        logger.LogWarning("File '{FilePath}' not found on disk, skipping", request.FilePath);
                        result = "skipped";
                    }

                    sw.Stop();
                    LoreMetrics.PipelineFilesProcessed.Add(1,
                        new KeyValuePair<string, object?>("pipeline.stage", "arrival"),
                        new KeyValuePair<string, object?>("result", result));
                    LoreMetrics.PipelineFileDuration.Record(sw.ElapsedMilliseconds,
                        new KeyValuePair<string, object?>("pipeline.stage", "arrival"));
                    return;
                }

                if (
                    existingFiles.TryGetValue(fileInfo.FullName, out var lastDbWriteTime)
                    && fileInfo.LastWriteTimeUtc == lastDbWriteTime
                )
                {
                    Interlocked.Increment(ref unchangedCount);
                    logger.FileUnchanged(fileInfo.FullName);
                    sw.Stop();
                    LoreMetrics.PipelineFilesProcessed.Add(1,
                        new KeyValuePair<string, object?>("pipeline.stage", "arrival"),
                        new KeyValuePair<string, object?>("result", "unchanged"));
                    LoreMetrics.PipelineFileDuration.Record(sw.ElapsedMilliseconds,
                        new KeyValuePair<string, object?>("pipeline.stage", "arrival"));
                    return;
                }

                using var fileStream = File.OpenRead(request.FilePath);
                var fileHash = await fileStream.ComputeSha256HexAsync();
                logger.FileHashed(request.FilePath, fileHash.Length);

                var entry = new FileEntry
                {
                    Name = fileInfo.Name,
                    Path = fileInfo.FullName,
                    Directory = fileInfo.DirectoryName ?? "",
                    Extension = fileInfo.Extension,
                    FileCreatedAt = fileInfo.CreationTimeUtc,
                    FileModifiedAt = fileInfo.LastWriteTimeUtc,
                    Size = fileInfo.Length,
                    Hash = fileHash,
                    ProcessStatus = FileProcessStatus.Pending,
                    CreatedAt = DateTime.UtcNow,
                    ModifiedAt = DateTime.UtcNow,
                };

                fileEntries.Add((entry, Activity.Current?.Id));

                sw.Stop();
                LoreMetrics.PipelineFilesProcessed.Add(1,
                    new KeyValuePair<string, object?>("pipeline.stage", "arrival"),
                    new KeyValuePair<string, object?>("result", "new"));
                LoreMetrics.PipelineFileDuration.Record(sw.ElapsedMilliseconds,
                    new KeyValuePair<string, object?>("pipeline.stage", "arrival"));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);

        var fileEntryList = fileEntries.Select(x => x.Entry).ToList();

        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await dbContext.Files
                    .Where(fl => filesToDelete.Contains(fl.Path))
                    .ExecuteDeleteAsync(cancellationToken);

                await dbContext.BulkInsertOrUpdateAsync(
                    fileEntryList,
                    new BulkConfig
                    {
                        UpdateByProperties = [nameof(FileEntry.Path)],
                        PropertiesToExcludeOnUpdate =
                        [
                            nameof(FileEntry.Id),
                            nameof(FileEntry.CreatedAt),
                        ],
                    },
                    cancellationToken: cancellationToken
                );
            });
        }

        logger.FileArrivalFinished(fileEntryList.Count, unchangedCount, filesToDelete.Count, skippedCount);

        if (fileEntryList.Count > 0)
        {
            logger.StageHandoff(fileEntryList.Count, "TextExtract");
            foreach (var (entry, traceParent) in fileEntries)
            {
                await textExtractChannel.Writer.WriteAsync(
                    new TextExtractRequest(entry.Path, traceParent),
                    cancellationToken
                );
            }
        }
    }
}