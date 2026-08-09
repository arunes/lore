using System.Collections.Concurrent;
using System.Threading.Channels;
using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Lore.Common.Helpers;
using Lore.Data;
using Lore.Data.Models;

namespace Lore.Core.Services;

public class FileArrivalService(
    ILogger<FileArrivalService> logger,
    Channel<TextExtractRequest> textExtractChannel,
    IDbContextFactory<LoreDbContext> dbContextFactory
) : IChannelService<FileArrivalRequest>
{
    public int GetBatchSize() => 250;

    public async Task ProcessAsync(
        FileArrivalRequest request,
        CancellationToken cancellationToken
    ) => await ProcessBatchAsync([request], cancellationToken);

    public async Task ProcessBatchAsync(
        IReadOnlyList<FileArrivalRequest> requests,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation(
            "Starting FileArrival process for {TotalFiles} files",
            requests.Count
        );

        var fileEntries = new ConcurrentBag<FileEntry>();
        var filesToDelete = new ConcurrentBag<string>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken,
        };

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

        await Parallel.ForEachAsync(
            requests,
            parallelOptions,
            async (request, ct) =>
            {
                var fileInfo = new FileInfo(request.FilePath);
                if (!fileInfo.Exists)
                {
                    if (existingFiles.ContainsKey(request.FilePath))
                    { // file is deleted
                        filesToDelete.Add(request.FilePath);
                        return;
                    }

                    logger.LogWarning(
                        "File '{FilePath}' can't be inspected, skipping",
                        request.FilePath
                    );
                    return;
                }

                // If file exists in db and hasn't been modified since, skip hashing
                if (
                    existingFiles.TryGetValue(fileInfo.FullName, out var lastDbWriteTime)
                    && fileInfo.LastWriteTimeUtc == lastDbWriteTime
                )
                {
                    logger.LogInformation(
                        "File '{FilePath}' has not changed, skipping",
                        fileInfo.FullName
                    );
                    return;
                }

                var fileHash = await HashHelpers.GetFileHashAsync(request.FilePath);
                fileEntries.Add(
                    new FileEntry
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
                    }
                );
            }
        );

        using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await dbContext.Files
                    .Where(fl => filesToDelete.Contains(fl.Path))
                    .ExecuteDeleteAsync(cancellationToken);

                await dbContext.BulkInsertOrUpdateAsync(
                    fileEntries,
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

        logger.LogInformation("FileArrival process finished, processed {FileCount} records", fileEntries.Count);
        foreach (var entry in fileEntries)
        {
            await textExtractChannel.Writer.WriteAsync(
                new TextExtractRequest(entry.Path),
                cancellationToken
            );
        }
    }
}
