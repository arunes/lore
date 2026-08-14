using Lore.Common.Models;
using Lore.Core.Logging;
using Lore.Core.Settings;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Lore.Core.RAG;

public class KernelFactory(
    IServiceProvider serviceProvider,
    IUserSettingsService userSettings,
    ILogger<KernelFactory> logger) : IKernelFactory
{
    public Kernel CreateKernel()
    {
        var model = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIModel);
        var endpoint = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIUrl);
        var apiKey = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIKey);

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(model, new Uri(endpoint), apiKey);

        var searchTools = serviceProvider.GetRequiredService<RetrievalTools>();
        builder.Plugins.AddFromObject(searchTools, pluginName: "RetrievalTools");

        var kernel = builder.Build();
        logger.KernelCreated(model, endpoint, 1);
        return kernel;
    }
}