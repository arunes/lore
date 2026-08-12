using Lore.Common.Models;
using Lore.Core.RAG;
using Lore.Core.Retrieval;
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
            .AddSingleton<IRetrievalService, RetrievalService>()
            .AddSingleton<IRAGFactory, RAGFactory>()
            .AddScoped<IKernelFactory, KernelFactory>()
            .AddScoped<KernelRetrievalTools>()
            .AddScoped<AgenticRAGService>()
            .AddKeyedTransient<IRAGService, TraditionalRAGService>(
                AIBackendRAGServiceType.Traditional
            )
            .AddKeyedTransient<IRAGService, AgenticRAGService>(
                AIBackendRAGServiceType.Agentic
            );
    }
}