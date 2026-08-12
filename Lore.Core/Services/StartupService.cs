using System.Collections.Concurrent;
using System.Threading.Channels;
using Lore.Core.LLM;
using Lore.Data;
using Lore.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lore.Core.Services;

public class StartupService(
    ILogger<StartupService> logger,
    ILogger<LoreWatcher> loreWatcherLogger,
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

            var watcher = new LoreWatcher(loreWatcherLogger, fileArrivalChannel, cancellationToken);
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

public class LoreWatcher(
    ILogger<LoreWatcher> logger,
    Channel<FileArrivalRequest> fileArrivalChannel,
    CancellationToken cancellationToken)
{
    private static readonly TimeSpan DebounceTime = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    private readonly ConcurrentDictionary<string, DateTime> _pending = new();
    private string[] _excludedExtensions = [];

    private void OnChangedOrCreated(object sender, FileSystemEventArgs e) =>
        _pending[e.FullPath] = DateTime.UtcNow;

    private void OnDeleted(object sender, FileSystemEventArgs e) => SendMessage(e.FullPath);
    private void OnRenamed(object sender, RenamedEventArgs e) => SendMessage(e.FullPath, e.OldFullPath);

    private void SendMessage(params string[] paths)
    {
        foreach (var path in paths)
        {
            var extension = Path.GetExtension(path);
            if (_excludedExtensions.Contains(extension))
            {
                logger.LogInformation("Skipping {Path} because it is ignored.", path);
                continue;
            }

            fileArrivalChannel.Writer.TryWrite(new FileArrivalRequest(path));
        }
    }

    private void OnError(object sender, ErrorEventArgs e) =>
        logger.LogError(e.GetException(), "Error occurred on file watcher");

    public async Task StartWatchingAsync(string directory, string? excludePattern)
    {
        _excludedExtensions = excludePattern?.Split(',') ?? [];

        using var watcher = new FileSystemWatcher(directory);

        watcher.Filter = "*.*";
        watcher.IncludeSubdirectories = true;
        watcher.NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size;

        watcher.Created += OnChangedOrCreated;
        watcher.Changed += OnChangedOrCreated;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;
        watcher.EnableRaisingEvents = true;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var flushTask = FlushPendingAsync(linkedCts.Token);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // expected shutdown
        }
        finally
        {
            linkedCts.Cancel();
            await flushTask;

            foreach (var (path, _) in _pending)
            {
                if (_pending.TryRemove(path, out _))
                    SendMessage(path);
            }
        }
    }

    private async Task FlushPendingAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await Task.Delay(PollInterval, cancellationToken);

                foreach (var (path, lastEvent) in _pending)
                {
                    if (DateTime.UtcNow - lastEvent < DebounceTime)
                    {
                        continue;
                    }

                    if (_pending.TryRemove(path, out _))
                    {
                        SendMessage(path);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // expected shutdown
        }
    }
}