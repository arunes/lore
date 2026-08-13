using Microsoft.Extensions.DependencyInjection;

namespace Lore.App.ErrorHandling;

public static class ErrorHandlingRegistration
{
    public static IServiceCollection AddLoreErrorHandling(this IServiceCollection services)
    {
        return services
            .AddProblemDetails()
            .AddExceptionHandler<LoreExceptionHandler>();
    }

    public static WebApplication UseLoreErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        return app;
    }
}
