using Lore.Data.Logging;
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

        logger.DbInitialized();
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}