using Microsoft.AspNetCore.Http;
using OneLoaf.ServiceDefaults.Security;
using Serilog.Core;
using Serilog.Events;

namespace OneLoaf.ServiceDefaults.Logging;

/// <summary>Adds <c>UserId</c> (the <c>sub</c> claim) to log events of authenticated requests. No e-mail or name (no PII).</summary>
public sealed class UserIdEnricher(IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    /// <summary>Name of the log property.</summary>
    public const string PropertyName = "UserId";

    /// <inheritdoc />
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userId = user.FindFirst(ClaimTypes.Subject)?.Value;
        if (!string.IsNullOrEmpty(userId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(PropertyName, userId));
        }
    }
}
