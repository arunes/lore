using System.ComponentModel;
using Spectre.Console.Cli;

namespace Lore.App.Commands;

internal class UICommand : AsyncCommand<UICommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("-p|--port")]
        [Description("The port number to start the server on")]
        [DefaultValue(8080)]
        public int Port { get; init; } = 8080;
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"https://*:{settings.Port}");
            builder.Services.AddAppServices();

            var app = builder.Build();
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.RegisterRoutes();

            Console.WriteLine($"Running web server on {settings.Port}...");
            await app.RunAsync(cancellationToken);
            return 0;
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception)
        {
            return 1;
        }
    }
}