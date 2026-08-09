using Lore.Core;
using Lore.Core.Services;
using Lore.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddOpenApi();
builder.Services.AddLoreServices();
builder.Services.AddLoreProcessors();
builder.Services.AddDataServices();

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