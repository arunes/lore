using Lore.Common.Models;
using Lore.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.RAG;

public class RAGFactory(
    IUserSettingsService userSettings,
    IServiceProvider serviceProvider) : IRAGFactory
{
    public IRAGService GetRAGService()
    {
        var serviceType = userSettings.GetSetting<AIBackendRAGServiceType>(UserSettingsType.AIBackendRAGService);
        return serviceProvider.GetRequiredKeyedService<IRAGService>(serviceType);
    }
}