using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;
using Lore.Core.Telemetry;

namespace Lore.Core.Pipeline;

public class ChannelProcessor<TRequest>(
    ILogger<ChannelProcessor<TRequest>> logger,
    Channel<TRequest> channel,
    IChannelService<TRequest> service
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Starting {Processor} processor", nameof(TRequest));

        var maxBatchSize = service.GetBatchSize();
        var timeout = TimeSpan.FromSeconds(2);
        var batch = new List<TRequest>(maxBatchSize);

        var stageName = typeof(TRequest).Name;

        while (await channel.Reader.WaitToReadAsync(stoppingToken))
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            cts.CancelAfter(timeout);

            try
            {
                while (batch.Count < maxBatchSize)
                {
                    var request = await channel.Reader.ReadAsync(cts.Token);
                    batch.Add(request);
                }
            }
            catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
            {
            }

            if (batch.Count > 0)
            {
                var sw = Stopwatch.StartNew();
                await service.ProcessBatchAsync(batch, stoppingToken);
                sw.Stop();

                logger.BatchDrained(batch.Count, stageName, sw.ElapsedMilliseconds);

                LoreMetrics.PipelineBatchSize.Record(batch.Count, new KeyValuePair<string, object?>("pipeline.stage", stageName));
                LoreMetrics.PipelineBatchDuration.Record(sw.ElapsedMilliseconds, new KeyValuePair<string, object?>("pipeline.stage", stageName));

                batch.Clear();
            }
        }
    }
}
