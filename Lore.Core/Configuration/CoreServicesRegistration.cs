using Lore.Core.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.Configuration;

public static class CoreServicesRegistration
{
    public static IServiceCollection AddLoreCore(this IServiceCollection services)
    {
        return services
            .AddSingleton<IUserSettingsService, UserSettingsService>()
            .AddOcrServices()
            .AddMCPServices()
            .AddTextExtractors()
            .AddRagServices()
            .AddPipelineServices();
    }
}