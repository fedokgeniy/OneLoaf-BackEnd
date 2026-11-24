namespace OneLoaf.Web.Shared.Api;

/// <summary>Values the handlers attach to a request so that <see cref="ApiClient"/> can read them afterwards.</summary>
public static class ApiRequestOptions
{
    /// <summary>The <c>X-Correlation-ID</c> sent with the request.</summary>
    public static readonly HttpRequestOptionsKey<string> CorrelationId = new("oneloaf.correlation-id");

    /// <summary>The <see cref="ApiError"/> built by <see cref="Handlers.ProblemDetailsHandler"/> for a non-2xx response.</summary>
    public static readonly HttpRequestOptionsKey<ApiError> Error = new("oneloaf.api-error");
}
