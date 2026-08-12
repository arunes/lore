using Lore.Common.Models;
using Lore.Core.LLM;
using Lore.Core.Services;

namespace Lore.App;

public static class Routes
{
    public static WebApplication RegisterRoutes(this WebApplication app)
    {
        var apiGroup = app.MapGroup("/api");

        apiGroup.MapPost(
            "chat",
            async (
                LoreChatRequest request,
                ILoreRAGFactory ragFactory,
                HttpResponse response,
                CancellationToken cancellationToken
            ) =>
            {
                var ragService = ragFactory.GetRAGService();
                var searchResult = await ragService.ChatAsync(request, cancellationToken);
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