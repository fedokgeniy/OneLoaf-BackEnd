using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.ServiceDefaults.Constants;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OneLoaf.ServiceDefaults.Observability;

/// <summary>
/// OpenTelemetry metrics exported for Prometheus on <c>/metrics</c>:
/// HTTP server and client, .NET runtime (GC, memory, thread pool), Npgsql pool and the custom OneLoaf meter.
/// W3C <c>traceparent</c> is propagated by <c>HttpClient</c>.
/// </summary>
public static class ObservabilityExtensions
{
    /// <summary>Registers the meters and the Prometheus exporter.</summary>
    public static IServiceCollection AddOneLoafObservability(this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(Meters.Npgsql, Meters.OneLoaf)
                .AddPrometheusExporter());
        return services;
    }

    /// <summary>
    /// Maps the scraping endpoint. It is anonymous inside the Docker network; the reverse proxy does not expose it.
    /// </summary>
    public static WebApplication MapOneLoafMetrics(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPrometheusScrapingEndpoint(Routes.Metrics).AllowAnonymous();
        return app;
    }
}
