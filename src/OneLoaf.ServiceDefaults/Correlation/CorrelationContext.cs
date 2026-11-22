namespace OneLoaf.ServiceDefaults.Correlation;

/// <summary>
/// Stores the correlation id in an <see cref="AsyncLocal{T}"/>, so it flows to everything called from the request,
/// including <c>HttpClient</c> handlers that the client factory creates outside of the request scope.
/// Registered as a singleton.
/// </summary>
public sealed class CorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<string?> Current = new();

    /// <inheritdoc />
    public string? CorrelationId => Current.Value;

    /// <inheritdoc />
    public void SetCorrelationId(string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        Current.Value = correlationId;
    }
}
