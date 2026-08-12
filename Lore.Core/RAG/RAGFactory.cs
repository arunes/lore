using Lore.Common.Models;
using Lore.Core.Logging;
using Lore.Core.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lore.Core.RAG;

public class RAGFactory(
    IUserSettingsService userSettings,
    IServiceProvider serviceProvider,
    ILogger<RAGFactory> logger) : IRAGFactory
{
    public IRAGService GetRAGService()
    {
        var serviceType = userSettings.GetSetting<AIBackendRAGServiceType>(UserSettingsType.AIBackendRAGService);
        var chatId = Guid.NewGuid().ToString("N")[..8];
        logger.LogDebug("Resolved RAG backend: {Backend}", serviceType);
        return serviceProvider.GetRequiredKeyedService<IRAGService>(serviceType);
    }
}