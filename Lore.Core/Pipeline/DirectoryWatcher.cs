using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;

namespace Lore.Core.Pipeline;

public class DirectoryWatcher(
    ILogger<DirectoryWatcher> logger,
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
                logger.WatcherIgnored(path);
                continue;
            }

            fileArrivalChannel.Writer.TryWrite(new FileArrivalRequest(path));
        }
    }

    private void OnError(object sender, ErrorEventArgs e) =>
        logger.WatcherError(e.GetException());

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
        }
    }
}