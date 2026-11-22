using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using OneLoaf.ServiceDefaults.Constants;
using Serilog.Context;

namespace OneLoaf.ServiceDefaults.Correlation;

/// <summary>
/// Reads <c>X-Correlation-ID</c> or creates a new one, stores it in <see cref="ICorrelationContext"/>, returns it in
/// the response header and adds it to the log context and the current <see cref="Activity"/> (architecture.md §7).
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ICorrelationContext correlation)
{
    /// <summary>Name of the log property and the Activity baggage item.</summary>
    public const string LogPropertyName = "CorrelationId";

    /// <summary>Longest accepted incoming correlation id; longer values are replaced.</summary>
    public const int MaxLength = 64;

    /// <summary>Processes the request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var incoming = context.Request.Headers[Headers.CorrelationId].ToString();
        var correlationId = IsValid(incoming) ? incoming : NewId();

        correlation.SetCorrelationId(correlationId);
        context.TraceIdentifier = correlationId;
        Activity.Current?.SetBaggage(LogPropertyName, correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[Headers.CorrelationId] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Accepts only short ids of letters, digits and <c>- _ . :</c>, so a client cannot inject arbitrary text into
    /// logs and headers.
    /// </summary>
    internal static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':');

    private static string NewId() => Guid.NewGuid().ToString("N");
}
