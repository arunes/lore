using Lore.App.Logging;
using Lore.Common.Models;
using Lore.Core.RAG;

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

        return app;
    }
}
