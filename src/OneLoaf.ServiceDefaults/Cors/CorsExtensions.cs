using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.ServiceDefaults.Constants;

namespace OneLoaf.ServiceDefaults.Cors;

/// <summary>
/// CORS for browser clients served from another origin (the web client in development on <c>http://localhost:5090</c>).
/// In production the web client and the APIs share one origin behind Caddy, so the list stays empty and no CORS
/// headers are sent.
/// </summary>
public static class CorsExtensions
{
    /// <summary>Configuration key: an array (<c>Cors:AllowedOrigins:0</c>) or one comma-separated string.</summary>
    public const string AllowedOriginsKey = "Cors:AllowedOrigins";

    /// <summary>Registers the default CORS policy for the configured origins.</summary>
    public static IServiceCollection AddOneLoafCors(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var origins = ReadOrigins(configuration);
        services.AddCors(options =>
        {
            if (origins.Length == 0)
            {
                return;
            }

            options.AddDefaultPolicy(policy => policy
                .WithOrigins(origins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                // The client shows the correlation id in error messages and waits Retry-After on 429/503.
                .WithExposedHeaders(Headers.CorrelationId, "Retry-After"));
        });
        return services;
    }

    /// <summary>Origins from configuration, trimmed, without empty entries or trailing slashes.</summary>
    public static string[] ReadOrigins(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var section = configuration.GetSection(AllowedOriginsKey);
        // An array (appsettings) and a delimited string (environment variable) can both be present: use both, so a
        // variable set for the container is not silently ignored when the settings file also lists origins.
        var values = section.GetChildren().Select(c => c.Value)
            .Concat((section.Value ?? string.Empty).Split([',', ';'], StringSplitOptions.None));
        return values
            .Select(v => v?.Trim().TrimEnd('/'))
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
