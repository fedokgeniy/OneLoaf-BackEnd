namespace OneLoaf.Web.Shared.Api.Handlers;

/// <summary>Adds a new <c>X-Correlation-ID</c> to every request; the same id is shown in error messages.</summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    /// <summary>Header name shared with the services.</summary>
    public const string HeaderName = "X-Correlation-ID";

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = Guid.NewGuid().ToString("N");
        request.Headers.Remove(HeaderName);
        request.Headers.TryAddWithoutValidation(HeaderName, id);
        request.Options.Set(ApiRequestOptions.CorrelationId, id);
        return base.SendAsync(request, cancellationToken);
    }
}
