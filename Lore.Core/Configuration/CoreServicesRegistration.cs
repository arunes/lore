using Lore.Core.Settings;
using Lore.Core.Files;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.Configuration;

public static class CoreServicesRegistration
{
    public static IServiceCollection AddLoreCore(this IServiceCollection services)
    {
        return services
            .AddSingleton<IUserSettingsService, UserSettingsService>()
            .AddScoped<IFileCatalogService, FileCatalogService>()
            .AddScoped<IFileSourceService, FileSourceService>()
            .AddOcrServices()
            .AddMCPServices()
            .AddTextExtractors()
            .AddRagServices()
            .AddPipelineServices();
    }
}
