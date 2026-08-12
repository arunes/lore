using Lore.App;
using Lore.Core.Configuration;
using Lore.Data;

var port = 8081;
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls($"https://*:{port}");
builder.Services.AddOpenApi()
            .AddLoreCore()
            .AddDataServices()
            .AddMemoryCache();

var app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.RegisterRoutes();

Console.WriteLine($"Running web server on {port}...");
await app.RunAsync();

