using Lore.App.Logging;
using Lore.Common.Models;
using Lore.Core.Files;
using Lore.Core.RAG;
using Lore.Core.Settings;

using Microsoft.AspNetCore.Mvc;

namespace Lore.App;

public static class Routes
{
    public static WebApplication RegisterRoutes(this WebApplication app)
    {
        ILogger apiLogger = app.Logger;
        RouteGroupBuilder apiGroup = app.MapGroup("/api");

        apiGroup.MapPost(
            "chat",
            async (
                LoreChatRequest request,
                IRAGFactory ragFactory,
                HttpResponse response,
                CancellationToken cancellationToken
            ) =>
            {
                string chatSid = (request.ChatId ?? Guid.NewGuid()).ToString("N")[..8];
                apiLogger.ChatRequestReceived(chatSid, request.Prompt.Length);

                IRAGService ragService = ragFactory.GetRAGService();
                LoreChatResponse searchResult = await ragService.ChatAsync(request, cancellationToken);
                response.ContentType = "text/plain; charset=utf-8";
                response.Headers["X-Chat-Id"] = searchResult.ChatId.ToString();
                await foreach (
                    string? token in searchResult.LLMResponseStream.WithCancellation(cancellationToken)
                )
                {
                    await response.WriteAsync(token, cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }
            }
        );

        apiGroup.MapGet(
            "files",
            async (
                [AsParameters] FileCatalogQuery query,
                IFileCatalogService fileCatalog,
                CancellationToken cancellationToken
            ) => Results.Ok(await fileCatalog.GetFilesAsync(query, cancellationToken))
        );

        apiGroup.MapGet(
            "settings",
            (IUserSettingsService userSettings) =>
            {
                var groups = SettingsCatalog.All
                    .GroupBy(d => d.Group)
                    .Select(group => new SettingsGroup(
                        group.Key.ToString(),
                        group.Select(d => new SettingMetadata(
                            d.Key.ToString(),
                            d.DisplayName,
                            d.Description,
                            d.Group.ToString(),
                            d.Widget.ToString(),
                            d.IsSecret,
                            d.IsRequired,
                            d.IsNullable,
                            d.Min,
                            d.Max,
                            d.Step,
                            userSettings.GetResolvedValue(d.Key),
                            d.DefaultValue?.ToString(),
                            d.Values,
                            HasOverride: userSettings.GetResolvedValue(d.Key) != d.DefaultValue?.ToString()
                        )).ToList()))
                    .ToList();

                return Results.Ok(new SettingsResponse(groups));
            }
        );

        apiGroup.MapPut(
            "settings",
            async (
                SettingsRequest request,
                IUserSettingsService userSettings,
                CancellationToken cancellationToken
            ) =>
            {
                var updates = new Dictionary<UserSettingsType, string?>();
                var errors = new Dictionary<string, string[]>();

                foreach (SettingValue item in request.Settings)
                {
                    if (!Enum.TryParse(item.Key, out UserSettingsType settingKey))
                    {
                        errors[item.Key] = [$"Unknown setting '{item.Key}'."];
                        continue;
                    }

                    updates[settingKey] = item.Value;
                }

                if (errors.Count > 0)
                {
                    return Results.ValidationProblem(errors);
                }

                await userSettings.SaveAsync(updates, cancellationToken);
                return Results.NoContent();
            }
        );

        app.MapMcp("/mcp");
        return app;
    }
}
