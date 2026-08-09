using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Lore.Core.LLM;
using Lore.Core.Services;
using Lore.Core.TextExtractors;
using SmartComponents.LocalEmbeddings;

namespace Lore.Core;

public static class ServiceHelpers
{
    public static IServiceCollection AddLoreServices(this IServiceCollection services)
    {
        // register code pages for NPOI doc parser
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return services
            .AddSingleton<ITextExtractorFactory, TextExtractorFactory>()
            .AddSingleton<EmbeddingCache>()
            .AddSingleton<LocalEmbedder>()
            .AddSingleton<ISearchService, SearchService>()
            .AddSingleton<IUserSettingsService, UserSettingsService>()
            .AddTransient<IChatClientFactory, ChatClientFactory>()
            .AddTransient<IChannelService<FileArrivalRequest>, FileArrivalService>()
            .AddTransient<IChannelService<TextExtractRequest>, TextExtractService>()
            .AddTransient<IChannelService<FileClassifyRequest>, FileClassifyService>()
            .AddTransient<IChannelService<ChunkingRequest>, ChunkingService>()
            .AddTransient<IChannelService<VectorizeRequest>, VectorizeService>();
    }

    public static IServiceCollection AddLoreProcessors(this IServiceCollection services)
    {
        services.AddSingleton(_ =>
            Channel.CreateUnbounded<FileArrivalRequest>(
                new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = false,
                }
            )
        );

        services.AddSingleton(_ =>
            Channel.CreateUnbounded<FileClassifyRequest>(
                new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = false,
                }
            )
        );

        services.AddSingleton(_ =>
            Channel.CreateUnbounded<TextExtractRequest>(
                new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = false,
                }
            )
        );

        services.AddSingleton(_ =>
            Channel.CreateUnbounded<VectorizeRequest>(
                new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = false,
                }
            )
        );

        services.AddSingleton(_ =>
            Channel.CreateUnbounded<ChunkingRequest>(
                new UnboundedChannelOptions
                {
                    SingleWriter = false,
                    SingleReader = false,
                    AllowSynchronousContinuations = false,
                }
            )
        );

        return services
            .AddHostedService<StartupService>()
            .AddHostedService<ChannelProcessor<FileArrivalRequest>>()
            .AddHostedService<ChannelProcessor<FileClassifyRequest>>()
            .AddHostedService<ChannelProcessor<TextExtractRequest>>()
            .AddHostedService<ChannelProcessor<VectorizeRequest>>()
            .AddHostedService<ChannelProcessor<ChunkingRequest>>();
    }
}
