using Lore.Core.Telemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Lore.App;

public static class TelemetryRegistration
{
    public static IServiceCollection AddLoreTelemetry(this IServiceCollection services,
        IConfiguration configuration)
    {
        var otlpEndpoint = configuration["Telemetry:Otlp:Endpoint"];
        if (string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            return services;
        }

        services.AddOpenTelemetry()
            .WithTracing(builder => builder
                .AddSource("Lore")
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(builder => builder
                .AddMeter("Lore")
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithLogging(logging => logging
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return services;
    }
}