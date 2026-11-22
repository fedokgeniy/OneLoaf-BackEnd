using OneLoaf.ServiceDefaults.Constants;

namespace OneLoaf.ServiceDefaults.Correlation;

/// <summary>
/// Adds the current correlation id to outgoing <c>HttpClient</c> requests (for example AI Assistant → Core API).
/// Register with <c>AddHttpClient(...).AddCorrelationIdPropagation()</c>.
/// </summary>
public sealed class CorrelationIdDelegatingHandler(ICorrelationContext correlation) : DelegatingHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = correlation.CorrelationId;
        if (!string.IsNullOrEmpty(correlationId) && !request.Headers.Contains(Headers.CorrelationId))
        {
            request.Headers.Add(Headers.CorrelationId, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
