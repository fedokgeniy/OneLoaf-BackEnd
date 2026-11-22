using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OneLoaf.ServiceDefaults.Correlation;
using OneLoaf.ServiceDefaults.Errors;
using OneLoaf.ServiceDefaults.Health;
using OneLoaf.ServiceDefaults.Logging;
using OneLoaf.ServiceDefaults.Observability;
using OneLoaf.ServiceDefaults.OpenApi;
using OneLoaf.ServiceDefaults.Security;

namespace OneLoaf.ServiceDefaults;

/// <summary>
/// Entry points used by every service:
/// <code>
/// builder.AddServiceDefaults(ServiceNames.CoreApi);
/// var app = builder.Build();
/// app.UseServiceDefaults();
/// app.MapServiceDefaults();
/// </code>
/// </summary>
public static class Extensions
{
    /// <summary>Registers logging, correlation id, error handling, JSON options, OpenAPI, security, health and metrics.</summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        builder.AddOneLoafLogging(serviceName);

        var services = builder.Services;
        services.AddCorrelationId();
        services.AddOneLoafErrorHandling();
        services.Configure<JsonOptions>(options => ConfigureJson(options.SerializerOptions));
        services.AddOneLoafOpenApi(serviceName);
        services.AddOneLoafSecurity();
        services.AddOneLoafHealthChecks();
        services.AddOneLoafObservability(serviceName);
        services.AddSingleton(TimeProvider.System);
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            // Only the reverse proxy (Caddy) is reachable from outside the Docker network, so its X-Forwarded-For is
            // trusted; the client IP is needed for per-IP rate limiting and logs.
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return builder;
    }

    /// <summary>
    /// Adds the middleware in the required order: forwarded headers (client IP) → correlation id (so every later log
    /// has it) → request logging →
    /// exception handler → status code pages (empty 401/404 become ProblemDetails) → Swagger UI (public static files)
    /// → authentication → authorization.
    /// </summary>
    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseOneLoafRequestLogging();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseOneLoafSwaggerUi();
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }

    /// <summary>Maps health checks, metrics and the OpenAPI document.</summary>
    public static WebApplication MapServiceDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapOneLoafHealthChecks();
        app.MapOneLoafMetrics();
        app.MapOneLoafOpenApi();
        return app;
    }

    /// <summary>JSON on the wire: camelCase properties, enums as snake_case strings like in the database (CLAUDE.md rule 10).</summary>
    public static void ConfigureJson(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
    }
}
