namespace OneLoaf.Web.Shared.Api;

/// <summary>Names of the three APIs; also the names of their <see cref="HttpClient"/>s.</summary>
public static class ServiceNames
{
    /// <summary>Core API.</summary>
    public const string Core = "core";

    /// <summary>AI Assistant.</summary>
    public const string Assistant = "assistant";

    /// <summary>Notification service.</summary>
    public const string Notification = "notification";

    /// <summary>All service names.</summary>
    public static IReadOnlyList<string> All { get; } = [Core, Assistant, Notification];
}

/// <summary>
/// Base URLs of the APIs from the <c>Api</c> section of <c>wwwroot/appsettings*.json</c>. Relative values
/// (<c>/core/</c>) are resolved against the address the app is served from (same origin behind Caddy).
/// </summary>
public sealed class ServiceEndpoints
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Api";

    /// <summary>Core API base URL, e.g. <c>/core/</c> or <c>http://localhost:5080/</c>.</summary>
    public string Core { get; set; } = string.Empty;

    /// <summary>Assistant base URL.</summary>
    public string Assistant { get; set; } = string.Empty;

    /// <summary>Notification base URL.</summary>
    public string Notification { get; set; } = string.Empty;

    /// <summary>Absolute base address of the named service; always ends with a slash.</summary>
    public Uri For(string serviceName, Uri appBaseAddress)
    {
        ArgumentNullException.ThrowIfNull(appBaseAddress);
        var value = serviceName switch
        {
            ServiceNames.Core => Core,
            ServiceNames.Assistant => Assistant,
            ServiceNames.Notification => Notification,
            _ => throw new ArgumentOutOfRangeException(nameof(serviceName), serviceName, "Unknown service."),
        };
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Api:{serviceName} is not configured in wwwroot/appsettings.json.");
        }

        if (!value.EndsWith('/'))
        {
            value += "/";
        }

        return new Uri(appBaseAddress, value);
    }
}
