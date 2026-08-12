using System.Collections.Concurrent;
using System.Threading.Channels;
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
    ILogger<DirectoryWatcher> loreWatcherLogger,
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

        await fileArrivalChannel.Writer.WriteAsync(
            new FileArrivalRequest("/home/arunes/downloads/ai200cert3.png"), stoppingToken);

        //await ResumeFiles(stoppingToken);
        //await WatchDirectories(stoppingToken);
    }

    private async Task WatchDirectories(CancellationToken cancellationToken)
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
                logger.LogWarning("{Directory} does not exists, skipping.", fileSource.Path);
                continue;
            }

            var watcher = new DirectoryWatcher(loreWatcherLogger, fileArrivalChannel, cancellationToken);
            tasks.Add(watcher.StartWatchingAsync(fileSource.Path, fileSource.ExcludePattern));
        }

        await Task.WhenAll(tasks);
    }


    private async Task ResumeFiles(CancellationToken cancellationToken)
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
            var task = file.ProcessStatus switch
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