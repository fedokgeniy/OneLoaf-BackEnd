namespace OneLoaf.ServiceDefaults.Constants;

/// <summary>Names of HTTP headers used by OneLoaf beyond the standard ones (api-design.md §1).</summary>
public static class Headers
{
    /// <summary>Correlation id of a request, accepted from the client or generated (requirement X7).</summary>
    public const string CorrelationId = "X-Correlation-ID";
}
