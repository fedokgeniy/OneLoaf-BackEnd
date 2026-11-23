using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

namespace OneLoaf.ServiceDefaults.Errors;

/// <summary>
/// Global error handling: every unhandled exception becomes <c>application/problem+json</c>
/// according to <see cref="ExceptionMapper"/>. Correlation id and instance are added by the ProblemDetails
/// customization, so framework-generated problems (401, 404) look the same.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>Extension with the exception type, added only in Development.</summary>
    public const string ExceptionTypeExtension = "exceptionType";

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var mapping = ExceptionMapper.Map(exception, httpContext.RequestAborted.IsCancellationRequested);
        Log(mapping, exception, httpContext);

        httpContext.Response.StatusCode = mapping.StatusCode;
        if (!mapping.WriteBody)
        {
            return true;
        }

        if (mapping.RetryAfter is { } retryAfter)
        {
            httpContext.Response.Headers[HeaderNames.RetryAfter] =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problem = mapping.Errors is null
            ? new ProblemDetails()
            : new ValidationProblemDetails(mapping.Errors.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal));
        problem.Status = mapping.StatusCode;
        problem.Type = mapping.Type;
        problem.Title = mapping.Title;
        problem.Detail = mapping.Detail;
        if (environment.IsDevelopment())
        {
            problem.Extensions[ExceptionTypeExtension] = exception.GetType().FullName;
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private void Log(ProblemMapping mapping, Exception exception, HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value ?? string.Empty;
        if (mapping.LogLevel >= LogLevel.Error)
        {
            LogUnhandled(logger, mapping.LogLevel, exception, httpContext.Request.Method, path, mapping.SqlState);
        }
        else if (logger.IsEnabled(mapping.LogLevel))
        {
            // Handled client errors: no stack trace, the message of an Application exception is client-safe.
            var exceptionType = exception.GetType().Name;
            LogHandled(logger, mapping.LogLevel, mapping.StatusCode, exceptionType,
                httpContext.Request.Method, path, mapping.SqlState);
        }
    }

    [LoggerMessage(Message = "Unhandled exception for {Method} {Path} (SQLSTATE {SqlState})")]
    private static partial void LogUnhandled(ILogger logger, LogLevel level, Exception exception, string method,
        string path, string? sqlState);

    [LoggerMessage(Message = "Request failed with {StatusCode}: {ExceptionType} for {Method} {Path} (SQLSTATE {SqlState})")]
    private static partial void LogHandled(ILogger logger, LogLevel level, int statusCode, string exceptionType,
        string method, string path, string? sqlState);
}
