using System.Globalization;
using OneLoaf.ServiceDefaults.Constants;

namespace OneLoaf.ServiceDefaults.Health;

/// <summary>
/// Command-line liveness probe for the Docker <c>HEALTHCHECK</c>: the ASP.NET runtime image has no curl, so the
/// service binary calls its own <c>/health/live</c> (<c>dotnet OneLoaf.CoreApi.dll --healthcheck</c>).
/// </summary>
public static class HealthProbe
{
    /// <summary>Argument that switches the binary to probe mode.</summary>
    public const string Argument = "--healthcheck";

    private const string HttpPortsVariable = "ASPNETCORE_HTTP_PORTS";
    private const int DefaultPort = 8080;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    /// <summary>True when the process was started as a probe.</summary>
    public static bool IsRequested(string[] args) => args is not null && args.Contains(Argument, StringComparer.Ordinal);

    /// <summary>Calls <c>/health/live</c> on the local port; returns 0 when healthy, 1 otherwise.</summary>
    public static async Task<int> RunAsync()
    {
        var port = ParsePort(Environment.GetEnvironmentVariable(HttpPortsVariable));
        using var client = new HttpClient { Timeout = Timeout };
        try
        {
            using var response = await client.GetAsync(new Uri($"http://localhost:{port}{Routes.HealthLive}"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }

    /// <summary>First port of <c>ASPNETCORE_HTTP_PORTS</c> (e.g. <c>8080;8081</c>), or 8080.</summary>
    internal static int ParsePort(string? httpPorts)
    {
        var first = httpPorts?.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is > 0 and <= 65535
            ? port
            : DefaultPort;
    }
}
