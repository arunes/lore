using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Lore.Core.LLM;
using Lore.Core.Services;
using Lore.Core.TextExtractors;
using SmartComponents.LocalEmbeddings;
using Lore.Common.Models;
using RapidOcrNet;
using Microsoft.Extensions.Hosting;

namespace Lore.Core;

public static class ServiceHelpers
{
    public static IServiceCollection AddOCRServices(this IServiceCollection services)
    {

        return services.AddSingleton(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            //var modelsPath = Path.Combine(env.ContentRootPath, "models");
            var modelsPath = "/home/arunes/repos/lore/Lore.Core/bin/Debug/net10.0/models/v5";

            var ocr = new RapidOcr();
            ocr.InitModels(
                detPath: Path.Combine(modelsPath, "ch_PP-OCRv5_mobile_det.onnx"),
                clsPath: Path.Combine(modelsPath, "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"),
                recPath: Path.Combine(modelsPath, "latin_PP-OCRv5_rec_mobile_infer.onnx"),
                keysPath: Path.Combine(modelsPath, "ppocrv5_latin_dict.txt")
            );

            return ocr;
        });
    }

    public static IServiceCollection AddAgenticServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<ILoreRAGFactory, LoreRAGFactory>()
            .AddScoped<IKernelFactory, KernelFactory>()
            .AddScoped<KernelSearchTools>()
            .AddScoped<AgenticRAGService>()
            .AddKeyedTransient<ILoreRAGService, TraditionalRAGService>(AIBackendRAGServiceType.Traditional)
            .AddKeyedTransient<ILoreRAGService, AgenticRAGService>(AIBackendRAGServiceType.Agentic);
    }

    public static IServiceCollection AddLoreServices(this IServiceCollection services)
    {
        // register code pages for NPOI doc parser
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        return services
            .AddSingleton<ITextExtractorFactory, TextExtractorFactory>()
            .AddSingleton<IUserSettingsService, UserSettingsService>()
            .AddSingleton<EmbeddingCache>()
            .AddSingleton<LocalEmbedder>()
            .AddSingleton<ILoreSearchTools, LoreSearchHelpers>()
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
