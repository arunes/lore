using Lore.Common.Models;
using Lore.Core.LLM;
using Microsoft.Extensions.DependencyInjection;
using SmartComponents.LocalEmbeddings;

namespace Lore.Core.Configuration;

public static class RagRegistration
{
    public static IServiceCollection AddRagServices(this IServiceCollection services)
    {
        return services
            .AddSingleton<EmbeddingCache>()
            .AddSingleton<LocalEmbedder>()
            .AddSingleton<ILoreSearchTools, LoreSearchHelpers>()
            .AddSingleton<ILoreRAGFactory, LoreRAGFactory>()
            .AddScoped<IKernelFactory, KernelFactory>()
            .AddScoped<KernelSearchTools>()
            .AddScoped<AgenticRAGService>()
            .AddKeyedTransient<ILoreRAGService, TraditionalRAGService>(
                AIBackendRAGServiceType.Traditional
            )
            .AddKeyedTransient<ILoreRAGService, AgenticRAGService>(
                AIBackendRAGServiceType.Agentic
            );
    }
}