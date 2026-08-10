using Lore.Common.Models;
using Lore.Core.Services;

namespace Lore.App;

public static class Routes
{
    public static WebApplication RegisterRoutes(this WebApplication app)
    {
        app.MapPost(
            "/ask",
            async (
                LoreChatRequest request,
                ILoreChatService searchService,
                HttpResponse response,
                CancellationToken cancellationToken
            ) =>
            {
                var searchResult = await searchService.ChatAsync(request, cancellationToken);
                response.ContentType = "text/plain; charset=utf-8";
                response.Headers["X-Chat-Id"] = searchResult.ChatId.ToString();
                await foreach (
                    var token in searchResult.LLMResponseStream.WithCancellation(cancellationToken)
                )
                {
                    await response.WriteAsync(token, cancellationToken);
                    await response.Body.FlushAsync(cancellationToken);
                }
            }
        );

        return app;
    }
}