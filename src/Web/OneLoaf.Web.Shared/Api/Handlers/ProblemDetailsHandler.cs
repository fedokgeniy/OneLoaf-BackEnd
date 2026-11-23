using System.Net.Http.Json;
using System.Text.Json;
using OneLoaf.Web.Shared.Auth;

namespace OneLoaf.Web.Shared.Api.Handlers;

/// <summary>
/// HTTP interceptor of the client: turns every non-2xx response into an <see cref="ApiError"/> attached to the request
/// (<see cref="ApiRequestOptions.Error"/>), and reports a 401 to a request that carried a token as an ended session.
/// </summary>
public sealed class ProblemDetailsHandler(AuthEvents authEvents, ApiJsonOptions json, TimeProvider time)
    : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = (int)response.StatusCode;
        request.Options.TryGetValue(ApiRequestOptions.CorrelationId, out var correlationId);
        var retryAfter = ReadRetryAfter(response);
        var problem = await ReadProblemAsync(response, cancellationToken);
        var error = problem is null
            ? ApiError.FromStatus(status, correlationId, retryAfter)
            : ApiError.FromProblem(problem, status, correlationId, retryAfter);
        request.Options.Set(ApiRequestOptions.Error, error);

        // A 401 without a token is a failed sign-in, not an ended session.
        if (status == 401 && request.Headers.Authorization is not null)
        {
            authEvents.RaiseUnauthorized();
        }

        return response;
    }

    private async Task<ProblemDetailsDto?> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is null || !mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            // In the browser the body is a one-shot stream; buffer it so HttpClient can still read it afterwards.
            await response.Content.LoadIntoBufferAsync(ct);
            return await response.Content.ReadFromJsonAsync(json.TypeInfo<ProblemDetailsDto>(), ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta;
        }

        if (header?.Date is { } date)
        {
            var wait = date - time.GetUtcNow();
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }
}
