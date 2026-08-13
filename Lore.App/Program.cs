using Lore.App;
using Lore.App.ErrorHandling;
using Lore.Core.Configuration;
using Lore.Data;

int port = 8081;
WebApplicationBuilder builder = WebApplication.CreateBuilder();
string urls = builder.Configuration["URLS"] ?? $"https://*:{port}";
builder.WebHost.UseUrls(urls);
builder.Services.AddOpenApi()
            .AddLoreErrorHandling()
            .AddDataServices()
            .AddLoreCore()
            .AddMemoryCache();

builder.Services.AddLoreTelemetry(builder.Configuration);

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseLoreErrorHandling();
app.UseStaticFiles();
app.RegisterRoutes();
app.MapFallbackToFile("index.html");

Console.WriteLine($"Running web server on {urls}...");
await app.RunAsync();

