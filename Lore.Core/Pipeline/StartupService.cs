using System.Threading.Channels;

using Lore.Core.Logging;
using Lore.Core.Retrieval;
using Lore.Core.Settings;
using Lore.Data;
using Lore.Data.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lore.Core.Pipeline;

public class StartupService(
    ILogger<StartupService> logger,
    ILogger<DirectoryWatcher> directoryWatcherLogger,
    IUserSettingsService userSettings,
    EmbeddingCache embeddingCache,
    LoreDbContext db,
    Channel<FileArrivalRequest> fileArrivalChannel,
    Channel<VectorizeRequest> vectorizeChannel,
    Channel<FileClassifyRequest> fileClassifyChannel,
    Channel<TextExtractRequest> textExtractChannel,
    Channel<ChunkingRequest> chunkingChannel
) : BackgroundService
{
    private record FileResumeItem(int Id, string Path, FileProcessStatus Status);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await userSettings.InitializeAsync(stoppingToken);
        await embeddingCache.InitializeAsync(stoppingToken);
        await ResumeFilesAsync(stoppingToken);
        WatchDirectoriesAsync(stoppingToken);

        logger.StartupComplete();
    }

    // private async Task FullScanDirectoriesAsync(CancellationToken cancellationToken)
    // {
    //     var fileSources = await db.FileSources
    //             .AsNoTracking()
    //             .Where(fs => fs.IsEnabled)
    //             .Select(fs => new { fs.Id, fs.Path, fs.ExcludePattern })
    //             .ToListAsync(cancellationToken);

    //     foreach (var fileSource in fileSources)
    //     {
    //         string[] excludedExtensions = fileSource.ExcludePattern?.Split(',') ?? [];
    //         if (!Directory.Exists(fileSource.Path))
    //         {
    //             logger.DirectoryMissing(fileSource.Path);
    //             continue;
    //         }

    //         string[] allFiles = Directory.GetFiles(fileSource.Path, "*.*", SearchOption.AllDirectories);
    //         foreach (string filePath in allFiles)
    //         {
    //             string extension = Path.GetExtension(filePath);
    //             if (excludedExtensions.Contains(extension))
    //             {
    //                 logger.WatcherIgnored(filePath);
    //                 continue;
    //             }

    //             await fileArrivalChannel.Writer.WriteAsync(new FileArrivalRequest(filePath), cancellationToken);
    //         }
    //     }
    // }

    private async Task WatchDirectoriesAsync(CancellationToken cancellationToken)
    {
        var fileSources = await db.FileSources
                .AsNoTracking()
                .Where(fs => fs.IsEnabled)
                .Select(fs => new { fs.Id, fs.Path, fs.ExcludePattern })
                .ToListAsync(cancellationToken);

        List<Task> tasks = [];
        foreach (var fileSource in fileSources)
        {
            if (!Directory.Exists(fileSource.Path))
            {
                logger.DirectoryMissing(fileSource.Path);
                continue;
            }

            logger.WatchDirectoryStarted(fileSource.Path, fileSource.ExcludePattern ?? "");
            var watcher = new DirectoryWatcher(directoryWatcherLogger, fileArrivalChannel, cancellationToken);
            tasks.Add(watcher.StartWatchingAsync(fileSource.Path, fileSource.ExcludePattern));
        }

        await Task.WhenAll(tasks);
    }


    private async Task ResumeFilesAsync(CancellationToken cancellationToken)
    {
        FileProcessStatus[] resumableStatuses = [
            FileProcessStatus.ChunksCreated,
            FileProcessStatus.Classified,
            FileProcessStatus.Pending,
            FileProcessStatus.TextExtracted
        ];

        var resumableFiles = db.Files
                .AsNoTracking()
                .Where(fl => resumableStatuses.Contains(fl.ProcessStatus))
                .Select(fl => new { fl.Id, fl.Path, fl.ProcessStatus })
                .AsAsyncEnumerable();

        await foreach (var file in resumableFiles.WithCancellation(cancellationToken))
        {
            ValueTask task = file.ProcessStatus switch
            {
                FileProcessStatus.ChunksCreated => vectorizeChannel.Writer.WriteAsync(new VectorizeRequest(file.Id), cancellationToken),
                FileProcessStatus.Classified => chunkingChannel.Writer.WriteAsync(new ChunkingRequest(file.Id), cancellationToken),
                FileProcessStatus.Pending => textExtractChannel.Writer.WriteAsync(new TextExtractRequest(file.Path), cancellationToken),
                FileProcessStatus.TextExtracted => fileClassifyChannel.Writer.WriteAsync(new FileClassifyRequest(file.Id), cancellationToken),
                _ => ValueTask.CompletedTask
            };

            await task;
        }
    }
}