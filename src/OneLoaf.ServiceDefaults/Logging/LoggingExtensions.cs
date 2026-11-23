using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneLoaf.ServiceDefaults.Constants;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.Grafana.Loki;

namespace OneLoaf.ServiceDefaults.Logging;

/// <summary>
/// Serilog setup: compact JSON to the console (collected from Docker), enriched with
/// <c>Service</c>, <c>Environment</c>, <c>CorrelationId</c> (log context) and <c>UserId</c>. Serilog records the
/// W3C trace and span ids of the current Activity itself (<c>@tr</c>, <c>@sp</c>). Levels come from the
/// <c>Serilog</c> configuration section (<c>Serilog__MinimumLevel__Default</c>).
/// </summary>
public static class LoggingExtensions
{
    /// <summary>Name of the log property with the service name.</summary>
    public const string ServicePropertyName = "Service";

    /// <summary>Name of the log property with the environment name.</summary>
    public const string EnvironmentPropertyName = "Environment";

    /// <summary>Configuration key of the Loki push URL (<c>Loki__Url</c>); logs go only to the console when empty.</summary>
    public const string LokiUrlKey = "Loki:Url";

    /// <summary>Replaces the default logging with Serilog.</summary>
    public static IHostApplicationBuilder AddOneLoafLogging(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<UserIdEnricher>();
        var lokiUrl = builder.Configuration[LokiUrlKey];
        builder.Services.AddSerilog((services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(builder.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty(ServicePropertyName, serviceName)
                .Enrich.WithProperty(EnvironmentPropertyName, builder.Environment.EnvironmentName)
                .Enrich.With(services.GetRequiredService<UserIdEnricher>())
                .WriteTo.Console(new RenderedCompactJsonFormatter());
            if (!string.IsNullOrWhiteSpace(lokiUrl))
            {
                // Low-cardinality labels only; correlation id, user id and trace id stay in the JSON body.
                configuration.WriteTo.GrafanaLoki(lokiUrl,
                    [
                        new LokiLabel { Key = "service", Value = serviceName },
                        new LokiLabel { Key = "environment", Value = builder.Environment.EnvironmentName },
                    ],
                    propertiesAsLabels: ["level"]);
            }
        });
        return builder;
    }

    /// <summary>One structured log entry per request; health and metrics requests are logged at Verbose.</summary>
    public static IApplicationBuilder UseOneLoafRequestLogging(this IApplicationBuilder app) =>
        app.UseSerilogRequestLogging(options => options.GetLevel = GetLevel);

    internal static LogEventLevel GetLevel(HttpContext context, double elapsedMilliseconds, Exception? exception)
    {
        if (exception is not null || context.Response.StatusCode >= StatusCodes.Status500InternalServerError)
        {
            return LogEventLevel.Error;
        }

        var path = context.Request.Path;
        return path.StartsWithSegments(Routes.HealthLive) || path.StartsWithSegments(Routes.HealthReady)
            || path.StartsWithSegments(Routes.Metrics)
            ? LogEventLevel.Verbose
            : LogEventLevel.Information;
    }
}
