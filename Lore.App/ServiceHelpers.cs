using Lore.Core;
using Lore.Data;

namespace Lore.App;

public static class ServiceHelpers
{
    public static IServiceCollection AddAppServices(this IServiceCollection services)
    {
        return services.AddOpenApi()
            .AddLoreServices()
            .AddLoreProcessors()
            .AddDataServices()
            .AddMemoryCache();
    }
}
