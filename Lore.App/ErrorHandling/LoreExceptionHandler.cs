using System.Diagnostics;

using Lore.App.Logging;
using Lore.Core.Settings;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Lore.App.ErrorHandling;

public sealed class LoreExceptionHandler(
    ILogger<LoreExceptionHandler> logger,
    IProblemDetailsService problemDetailsService
) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        // Response has already started (e.g. mid-stream), we can no longer rewrite it.
        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        string traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;

        if (exception is MissingRequiredSettingException missing)
        {
            logger.UnhandledException(exception, traceId);
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.Headers["X-Correlation-Id"] = traceId;

            return await problemDetailsService.TryWriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    Exception = exception,
                    ProblemDetails = new ProblemDetails
                    {
                        Status = StatusCodes.Status400BadRequest,
                        Title = "Missing required setting",
                        Detail = missing.Message,
                        Extensions =
                        {
                            ["setting"] = missing.Setting.ToString(),
                            ["traceId"] = traceId,
                        },
                    },
                }
            );
        }

        logger.UnhandledException(exception, traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.Headers["X-Correlation-Id"] = traceId;

        return await problemDetailsService.TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = httpContext,
                Exception = exception,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An error occurred while processing your request.",
                    Detail = "The server encountered an unexpected condition and could not complete your request.",
                    Extensions =
                    {
                        ["traceId"] = traceId,
                    },
                },
            }
        );
    }
}
