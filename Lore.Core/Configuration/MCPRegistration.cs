using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.Configuration;

public static class MCPRegistration
{
    public static IServiceCollection AddMCPServices(this IServiceCollection services)
    {
        services.AddMcpServer()
            .WithHttpTransport()
            .WithToolsFromAssembly();

        return services;
    }
}