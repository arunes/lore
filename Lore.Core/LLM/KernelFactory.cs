using Lore.Common.Models;
using Lore.Core.Services;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.LLM;

public interface IKernelFactory
{
    Kernel CreateKernel();
}

public class KernelFactory(
    IServiceProvider serviceProvider,
    IUserSettingsService userSettings) : IKernelFactory
{
    public Kernel CreateKernel()
    {
        var builder = Kernel.CreateBuilder();

        builder.AddOpenAIChatCompletion(
            userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIModel),
            new Uri(userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIUrl)),
            userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIKey)
        );

        var searchTools = serviceProvider.GetRequiredService<KernelSearchTools>();
        builder.Plugins.AddFromObject(searchTools, pluginName: "SearchTools");

        return builder.Build();
    }
}