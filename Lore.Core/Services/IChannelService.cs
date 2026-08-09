namespace Lore.Core.Services;

public interface IChannelService<T>
{
    int GetBatchSize();

    Task ProcessAsync(T request, CancellationToken cancellationToken);

    Task ProcessBatchAsync(IReadOnlyList<T> requests, CancellationToken cancellationToken);
}