using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace OneLoaf.Web.Shared.Api;

/// <summary>
/// Typed access to one API over the shared handler pipeline. Returns <see cref="ApiResult{T}"/> /
/// <see cref="ApiResult"/> and never throws for an HTTP or network error. Paths are relative, without a leading slash
/// (<c>api/v1/recipes</c>). Only feature <c>Services</c> classes use it (architecture.md §2).
/// </summary>
public sealed partial class ApiClient(HttpClient http, ApiJsonOptions json, ILogger<ApiClient> logger)
{
    /// <summary>GET returning a body.</summary>
    public Task<ApiResult<T>> GetAsync<T>(string path, CancellationToken ct = default) =>
        SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, path), ct);

    /// <summary>POST with a body, returning a body.</summary>
    public Task<ApiResult<T>> PostAsync<TBody, T>(string path, TBody body, CancellationToken ct = default) =>
        SendAsync<T>(WithBody(HttpMethod.Post, path, body), ct);

    /// <summary>POST with a body, no response body (204).</summary>
    public Task<ApiResult> PostAsync<TBody>(string path, TBody body, CancellationToken ct = default) =>
        SendAsync(WithBody(HttpMethod.Post, path, body), ct);

    /// <summary>POST without a body and without a response body (e.g. logout).</summary>
    public Task<ApiResult> PostAsync(string path, CancellationToken ct = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Post, path), ct);

    /// <summary>PUT with a body, returning a body.</summary>
    public Task<ApiResult<T>> PutAsync<TBody, T>(string path, TBody body, CancellationToken ct = default) =>
        SendAsync<T>(WithBody(HttpMethod.Put, path, body), ct);

    /// <summary>PATCH with a body, no response body.</summary>
    public Task<ApiResult> PatchAsync<TBody>(string path, TBody body, CancellationToken ct = default) =>
        SendAsync(WithBody(HttpMethod.Patch, path, body), ct);

    /// <summary>DELETE, no response body.</summary>
    public Task<ApiResult> DeleteAsync(string path, CancellationToken ct = default) =>
        SendAsync(new HttpRequestMessage(HttpMethod.Delete, path), ct);

    private HttpRequestMessage WithBody<TBody>(HttpMethod method, string path, TBody body) =>
        new(method, path) { Content = JsonContent.Create(body, json.TypeInfo<TBody>()) };

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        {
            var (response, error) = await TrySendAsync(request, ct);
            using (response)
            {
                if (error is not null)
                {
                    return ApiResult<T>.Fail(error);
                }

                try
                {
                    var value = await response!.Content.ReadFromJsonAsync(json.TypeInfo<T>(), ct);
                    return value is null
                        ? ApiResult<T>.Fail(ApiError.FromStatus(500, CorrelationIdOf(request)))
                        : ApiResult<T>.Ok(value);
                }
                catch (JsonException ex)
                {
                    LogUnreadableBody(logger, request.RequestUri?.AbsolutePath ?? string.Empty, ex.Message);
                    return ApiResult<T>.Fail(ApiError.FromStatus(500, CorrelationIdOf(request)));
                }
            }
        }
    }

    private async Task<ApiResult> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        using (request)
        {
            var (response, error) = await TrySendAsync(request, ct);
            response?.Dispose();
            return error is null ? ApiResult.Ok : ApiResult.Fail(error);
        }
    }

    /// <summary>Sends the request; on failure returns the error built by the handlers or a network error.</summary>
    private async Task<(HttpResponseMessage? Response, ApiError? Error)> TrySendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (HttpRequestException)
        {
            return (null, ApiError.Network(CorrelationIdOf(request)));
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            // Timeout of the HttpClient, not a cancellation by the caller.
            return (null, ApiError.Network(CorrelationIdOf(request)));
        }

        if (response.IsSuccessStatusCode)
        {
            return (response, null);
        }

        var error = request.Options.TryGetValue(ApiRequestOptions.Error, out var attached)
            ? attached
            : ApiError.FromStatus((int)response.StatusCode, CorrelationIdOf(request));
        return (response, error);
    }

    private static string? CorrelationIdOf(HttpRequestMessage request) =>
        request.Options.TryGetValue(ApiRequestOptions.CorrelationId, out var id) ? id : null;

    [LoggerMessage(Level = LogLevel.Error, Message = "Response of {Path} could not be read: {Reason}")]
    private static partial void LogUnreadableBody(ILogger logger, string path, string reason);
}
