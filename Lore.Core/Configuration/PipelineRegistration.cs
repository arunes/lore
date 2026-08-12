using System.Threading.Channels;
using Lore.Core.Pipeline;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.Configuration;

public static class PipelineRegistration
{
    public static IServiceCollection AddPipelineServices(this IServiceCollection services)
    {
        return services
            .AddPipelineStage<FileArrivalRequest, FileArrivalService>()
            .AddPipelineStage<FileClassifyRequest, FileClassifyService>()
            .AddPipelineStage<TextExtractRequest, TextExtractService>()
            .AddPipelineStage<VectorizeRequest, VectorizeService>()
            .AddPipelineStage<ChunkingRequest, ChunkingService>()
            .AddHostedService<StartupService>();
    }

    private static IServiceCollection AddPipelineStage<TRequest, TService>(
        this IServiceCollection services
    )
        where TRequest : class
        where TService : class, IChannelService<TRequest>
    {
        services.AddSingleton(_ =>
            Channel.CreateUnbounded<TRequest>(
                new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = false,
                }
            )
        );

        return services
            .AddTransient<IChannelService<TRequest>, TService>()
            .AddHostedService<ChannelProcessor<TRequest>>();
    }
}