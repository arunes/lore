using Lore.Common.Models;
using Lore.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.LLM;

public interface ILoreRAGFactory
{
    public ILoreRAGService GetRAGService();
}

public class LoreRAGFactory(
    IUserSettingsService userSettings,
    IServiceProvider serviceProvider) : ILoreRAGFactory
{
    public ILoreRAGService GetRAGService()
    {
        var serviceType = userSettings.GetSetting<AIBackendRAGServiceType>(UserSettingsType.AIBackendRAGService);
        return serviceProvider.GetRequiredKeyedService<ILoreRAGService>(serviceType);
    }
}