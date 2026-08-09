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
        logger.LogInformation("Initializing SQLite Database and vector extensions...");
        using var dbContext = await dbContextFactory.CreateVectorDbContextAsync(cancellationToken);

        // Ensure EF migrations are applied
        await dbContext.Database.MigrateAsync(cancellationToken);

        // Ensure sqlite-vec virtual tables exist
        await dbContext.EnsureVectorTablesCreatedAsync(cancellationToken);

        // Ensure FTS tables exist
        await dbContext.EnsureFTSTablesCreatedAsync(cancellationToken);

        logger.LogInformation("Database vector tables initialized successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
