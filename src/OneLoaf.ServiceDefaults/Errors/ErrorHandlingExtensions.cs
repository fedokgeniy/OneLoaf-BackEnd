using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.ServiceDefaults.Correlation;

namespace OneLoaf.ServiceDefaults.Errors;

/// <summary>Registration of ProblemDetails and the global exception handler.</summary>
public static class ErrorHandlingExtensions
{
    /// <summary>Extension with the correlation id in every problem response.</summary>
    public const string CorrelationIdExtension = "correlationId";

    private const string FrameworkTypePrefix = "https://tools.ietf.org/html/rfc9110";

    /// <summary>
    /// Problem types of responses produced by the framework itself (authentication challenge, authorization, unmatched
    /// route, parameter binding), so every error uses the OneLoaf types.
    /// </summary>
    private static readonly Dictionary<int, (string Type, string Title)> FrameworkProblems = new()
    {
        [StatusCodes.Status400BadRequest] = (ProblemTypes.BadRequest, "The request is invalid."),
        [StatusCodes.Status401Unauthorized] = (ProblemTypes.Unauthorized, "A valid bearer token is required."),
        [StatusCodes.Status403Forbidden] = (ProblemTypes.Forbidden, "You are not allowed to perform this action."),
        [StatusCodes.Status404NotFound] = (ProblemTypes.NotFound, "The requested resource was not found."),
    };

    /// <summary>Adds ProblemDetails (RFC 9457) with instance and correlation id, and <see cref="GlobalExceptionHandler"/>.</summary>
    public static IServiceCollection AddOneLoafErrorHandling(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            var problem = context.ProblemDetails;
            problem.Instance ??= context.HttpContext.Request.Path;
            if ((problem.Type is null || problem.Type.StartsWith(FrameworkTypePrefix, StringComparison.Ordinal))
                && problem.Status is { } status && FrameworkProblems.TryGetValue(status, out var known))
            {
                problem.Type = known.Type;
                problem.Title = known.Title;
            }

            var correlationId = context.HttpContext.RequestServices.GetService<ICorrelationContext>()?.CorrelationId;
            if (!string.IsNullOrEmpty(correlationId))
            {
                problem.Extensions[CorrelationIdExtension] = correlationId;
            }
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();
        return services;
    }
}
