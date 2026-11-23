using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.ServiceDefaults;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>
/// Typed <c>HttpClient</c> for the Core API. Forwards the caller's bearer token (so tools run with the baker's own
/// rights) and, through the registered handler, the correlation id.
/// </summary>
public sealed class CoreApiClient(HttpClient http, IHttpContextAccessor httpContextAccessor) : ICoreApiClient
{
    private const string BearerScheme = "Bearer";
    private const string UnavailableMessage = "The Core API is temporarily unavailable.";
    private static readonly JsonSerializerOptions Json = CreateJson();

    /// <inheritdoc />
    public async Task<CoreApiResponse> SendAsync(HttpMethod method, string pathAndQuery, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, pathAndQuery);
        var authorization = httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].ToString();
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith(BearerScheme + " ", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, authorization[(BearerScheme.Length + 1)..]);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: Json);
        }

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            return new CoreApiResponse(response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(UnavailableMessage, ex);
        }
    }

    private static JsonSerializerOptions CreateJson()
    {
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Extensions.ConfigureJson(json);
        return json;
    }
}
