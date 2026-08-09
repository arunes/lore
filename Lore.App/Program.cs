using System.Threading.Channels;
using Lore.Core;
using Lore.Core.LLM;
using Lore.Core.Services;
using Lore.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddOpenApi();
builder.Services.AddLoreServices();
builder.Services.AddLoreProcessors();
builder.Services.AddDataServices();
builder.Services.AddHostedService<DummyService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet(
    "/ask",
    async (
        string query,
        bool multiQuery,
        ISearchService searchService,
        IUserSettingsService userSettings,
        HttpResponse response,
        CancellationToken cancellationToken
    ) =>
    {
        var searchResult = await searchService.SearchAsync(query, multiQuery, cancellationToken);
        response.ContentType = "text/plain; charset=utf-8";
        //response.Headers["X-Top-Chunk-Ids"] = string.Join(",", searchResult.TopChunkIds);
        await foreach (
            var token in searchResult.LLMResponseStream.WithCancellation(cancellationToken)
        )
        {
            await response.WriteAsync(token, cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }
);
await app.RunAsync();

public class DummyService(
    IUserSettingsService userSettings,
    EmbeddingCache embeddingCache,
    Channel<FileArrivalRequest> channel,
    LoreDbContext db
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await userSettings.InitializeAsync(stoppingToken);
        await embeddingCache.InitializeAsync(stoppingToken);

        //await Task.Delay(1000);

        //await channel.Writer.WriteAsync(new VectorizeRequest(1), stoppingToken);

        // var fileRequests = Directory
        //     .GetFiles("/mnt/data/Documents", "*.*", SearchOption.AllDirectories)
        //     .Select(path => new FileArrivalRequest(path));

        // foreach (var request in fileRequests)
        // {
        //     await channel.Writer.WriteAsync(request, stoppingToken);
        // }
    }
}

/*
        // 1. Exact FTS Keyword Search
        var ftsIds = await dbContext
            .Database.SqlQueryRaw<int>(
                $@"
            SELECT rowid
            FROM file_chunks_fts
            WHERE file_chunks_fts MATCH '{sanitized}'
            ORDER BY rank
            LIMIT 10"
            )
            .ToListAsync(cancellationToken);

        List<int> combinedIds;

        // 2. If FTS finds matches, prioritize them. Only fallback to vector search if FTS hits < 10
        if (ftsIds.Count >= 10)
        {
            combinedIds = ftsIds;
        }
        else
        {
            var queryJson = JsonSerializer.Serialize(embedder.Embed(query).Values.ToArray());
            var vectorIds = await dbContext
                .Database.SqlQueryRaw<int>(
                    @"
                SELECT chunk_id
                FROM vec_file_chunks
                WHERE embedding MATCH {0} AND k = 10
                ORDER BY distance ASC",
                    queryJson
                )
                .ToListAsync(cancellationToken);

            // Combine: FTS results first, then distinct vector results
            combinedIds = ftsIds.Concat(vectorIds.Except(ftsIds)).Take(10).ToList();
        }

        if (combinedIds.Count == 0)
            return Results.Ok(Array.Empty<object>());

        // 3. Hydrate rows from EF Core
        var unmappedChunks = await dbContext
            .FileChunks.AsNoTracking()
            .Where(c => combinedIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

        // 4. CRITICAL: Preserve the rank order of combinedIds (EF Core 'IN' clause resets order to Id ASC)
        var results = combinedIds
            .Select(id => unmappedChunks.FirstOrDefault(c => c.Id == id))
            .Where(c => c != null)
            .ToList();

        return Results.Ok(results);
*/
