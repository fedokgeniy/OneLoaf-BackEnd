using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using OneLoaf.ServiceDefaults.Errors;
using OneLoaf.ServiceDefaults.Options;

namespace OneLoaf.CoreApi.RateLimiting;

/// <summary>Names of the rate limiting policies.</summary>
public static class RateLimitPolicies
{
    /// <summary>Fixed window per client IP on <c>/auth/*</c> (brute force protection).</summary>
    public const string Auth = "auth";
}

/// <summary>Limits of <see cref="RateLimitPolicies.Auth"/> (<c>RateLimiting__Auth__PermitLimit</c>, ...).</summary>
public sealed class AuthRateLimitOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "RateLimiting:Auth";

    /// <summary>Requests allowed per window and client IP.</summary>
    [Range(1, 10_000)]
    public int PermitLimit { get; set; } = 10;

    /// <summary>Window length in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 60;
}

/// <summary>Registration of the rate limiter.</summary>
public static class AuthRateLimiting
{
    private const string UnknownClient = "unknown";
    private const string TooManyRequestsTitle = "Too many requests. Try again later.";

    /// <summary>Adds the fixed-window limiter for the authentication endpoints; rejections are 429 ProblemDetails.</summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services)
    {
        services.AddValidatedOptions<AuthRateLimitOptions>(AuthRateLimitOptions.SectionName);
        services.AddRateLimiter(limiter =>
        {
            limiter.AddPolicy(RateLimitPolicies.Auth, httpContext =>
            {
                var options = httpContext.RequestServices.GetRequiredService<IOptions<AuthRateLimitOptions>>().Value;
                var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClient;
                return RateLimitPartition.GetFixedWindowLimiter(clientIp, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = options.PermitLimit,
                    Window = TimeSpan.FromSeconds(options.WindowSeconds),
                    QueueLimit = 0,
                });
            });
            limiter.OnRejected = WriteRejectionAsync;
        });
        return services;
    }

    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers[HeaderNames.RetryAfter] =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problemDetails = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
        await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Type = ProblemTypes.TooManyRequests,
                Title = TooManyRequestsTitle,
            },
        });
    }
}
