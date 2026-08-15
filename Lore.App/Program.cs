using Lore.App;
using Lore.App.ErrorHandling;
using Lore.Core.Configuration;
using Lore.Data;

WebApplicationBuilder builder = WebApplication.CreateBuilder();
builder.Services.AddOpenApi()
            .AddLoreErrorHandling()
            .AddDataServices()
            .AddLoreCore()
            .AddMemoryCache()
            .AddLoreTelemetry(builder.Configuration);

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseLoreErrorHandling();
app.UseStaticFiles();
app.RegisterRoutes();
app.MapFallbackToFile("index.html");

await app.RunAsync();
