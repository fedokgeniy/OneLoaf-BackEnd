using System.Net;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary>A response of the Core API as seen by a tool.</summary>
/// <param name="Status">HTTP status.</param>
/// <param name="Body">Response body (JSON or empty).</param>
public sealed record CoreApiResponse(HttpStatusCode Status, string Body)
{
    /// <summary>2xx.</summary>
    public bool IsSuccess => (int)Status is >= 200 and < 300;
}

/// <summary>
/// The Core API, called on behalf of the current user: implementations forward the user's bearer token and the
/// correlation id, so a tool sees exactly what the baker can see.
/// </summary>
public interface ICoreApiClient
{
    /// <summary>Sends a request; transport failures are mapped to <c>ExternalServiceUnavailableException</c>.</summary>
    Task<CoreApiResponse> SendAsync(HttpMethod method, string pathAndQuery, object? body, CancellationToken cancellationToken);
}
