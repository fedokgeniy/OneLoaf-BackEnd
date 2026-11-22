using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.ServiceDefaults.Correlation;

namespace OneLoaf.ServiceDefaults.Errors;

/// <summary>Registration of ProblemDetails and the global exception handler.</summary>
public static class ErrorHandlingExtensions
{
    /// <summary>Extension with the correlation id in every problem response (api-design.md §2).</summary>
    public const string CorrelationIdExtension = "correlationId";

    /// <summary>Adds ProblemDetails (RFC 9457) with instance and correlation id, and <see cref="GlobalExceptionHandler"/>.</summary>
    public static IServiceCollection AddOneLoafErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
            var correlationId = context.HttpContext.RequestServices.GetService<ICorrelationContext>()?.CorrelationId;
            if (!string.IsNullOrEmpty(correlationId))
            {
                context.ProblemDetails.Extensions[CorrelationIdExtension] = correlationId;
            }
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }
}
