using Lore.Common;
using Lore.Data.Logging;
using Lore.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Lore.Data;

public class DbInitializerHostedService(
    IDbContextFactory<LoreDbContext> dbContextFactory,
    ILogger<DbInitializerHostedService> logger
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.DbInitializing();
        using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(cancellationToken);

        await dbContext.Database.MigrateAsync(cancellationToken);
        logger.MigrationsApplied();

        await dbContext.EnsureVectorTablesCreatedAsync(cancellationToken);
        logger.VectorTablesEnsured();

        await dbContext.EnsureFTSTablesCreatedAsync(cancellationToken);
        logger.FtsTablesEnsured();

        if (LorePaths.IsDocker)
        {
            await SeedFileSourcesFromDataDirAsync(dbContext, cancellationToken);
        }

        logger.DbInitialized();
    }

    private static async Task SeedFileSourcesFromDataDirAsync(
        LoreDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        if (!Directory.Exists(LorePaths.UserDataDir))
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        var existingPaths = await dbContext
            .FileSources.Select(fs => fs.Path)
            .ToHashSetAsync(cancellationToken);

        foreach (string directory in Directory.EnumerateDirectories(LorePaths.UserDataDir))
        {
            if (existingPaths.Contains(directory))
            {
                continue;
            }

            dbContext.FileSources.Add(
                new FileSource
                {
                    Path = directory,
                    IsEnabled = true,
                    CreatedAt = now,
                    ModifiedAt = now,
                }
            );
            existingPaths.Add(directory);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}