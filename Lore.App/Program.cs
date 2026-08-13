using Lore.App;
using Lore.App.ErrorHandling;
using Lore.Core.Configuration;
using Lore.Data;

int port = 8081;
WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.WebHost.UseUrls($"https://*:{port}");
builder.Services.AddOpenApi()
            .AddLoreErrorHandling()
            .AddLoreCore()
            .AddDataServices()
            .AddMemoryCache();

builder.Services.AddLoreTelemetry(builder.Configuration);

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseLoreErrorHandling();
app.RegisterRoutes();

Console.WriteLine($"Running web server on {port}...");
await app.RunAsync();

