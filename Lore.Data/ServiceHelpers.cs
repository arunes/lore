using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lore.Data;

public static class ServiceHelpers
{
    public static IServiceCollection AddDataServices(this IServiceCollection services)
    {
        var dbPath =
            $"Data Source={GetDatabasePath()};Cache=Shared;Mode=ReadWriteCreate;Pooling=True;";
        
        services.AddDbContext<LoreDbContext>(options =>
            options.UseSqlite(dbPath).UseSnakeCaseNamingConvention()
        );
        
        services.AddDbContextFactory<LoreDbContext>(options =>
            options.UseSqlite(dbPath).UseSnakeCaseNamingConvention()
        );

        return services.AddHostedService<DbInitializerHostedService>();
    }

    private static string GetDatabasePath()
    {
        string fileName = "lore.db";

#if DEBUG
        string baseDir = AppContext.BaseDirectory;
        string solutionRoot = Path.GetFullPath(Path.Combine(baseDir, @"../../../.."));
        return Path.Combine(solutionRoot, fileName);
#else
        return Path.Combine(AppContext.BaseDirectory, fileName);
#endif
    }
}
