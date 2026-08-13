using Lore.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Data;

public static class ServiceHelpers
{
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        LorePaths.EnsureDirectories();
        var dbPath =
            $"Data Source={LorePaths.DatabasePath};Cache=Shared;Mode=ReadWriteCreate;Pooling=True;";
        
        services.AddDbContext<LoreDbContext>(options =>
            options.UseSqlite(dbPath).UseSnakeCaseNamingConvention()
        );
        
        services.AddDbContextFactory<LoreDbContext>(options =>
            options.UseSqlite(dbPath).UseSnakeCaseNamingConvention()
        );

        return services.AddHostedService<DbInitializerHostedService>();
    }
}
