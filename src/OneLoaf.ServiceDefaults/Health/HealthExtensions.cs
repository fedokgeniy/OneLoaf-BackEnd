using System.Net.Mime;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OneLoaf.ServiceDefaults.Constants;

namespace OneLoaf.ServiceDefaults.Health;

/// <summary>
/// Liveness and readiness endpoints. Services add their dependency checks with tag
/// <see cref="HealthTags.Ready"/>; <c>/health/live</c> checks only the process.
/// </summary>
public static class HealthExtensions
{
    /// <summary>Name of the process check.</summary>
    public const string SelfCheckName = "self";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Registers the health check service with the process check.</summary>
    public static IHealthChecksBuilder AddOneLoafHealthChecks(this IServiceCollection services) =>
        services.AddHealthChecks()
            .AddCheck(SelfCheckName, () => HealthCheckResult.Healthy(), tags: [HealthTags.Live]);

    /// <summary>Maps <c>/health/live</c> and <c>/health/ready</c>; both are anonymous.</summary>
    public static WebApplication MapOneLoafHealthChecks(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks(Routes.HealthLive, Options(HealthTags.Live)).AllowAnonymous();
        app.MapHealthChecks(Routes.HealthReady, Options(HealthTags.Ready)).AllowAnonymous();
        return app;
    }

    private static HealthCheckOptions Options(string tag) => new()
    {
        Predicate = registration => registration.Tags.Contains(tag),
        ResponseWriter = WriteResponseAsync,
    };

    /// <summary>Status per check and duration; no exception messages (they may contain host names).</summary>
    internal static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = MediaTypeNames.Application.Json;
        var body = new
        {
            status = report.Status.ToString(),
            durationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = Math.Round(e.Value.Duration.TotalMilliseconds, 1),
            }),
        };
        return JsonSerializer.SerializeAsync(context.Response.Body, body, JsonOptions, context.RequestAborted);
    }
}
