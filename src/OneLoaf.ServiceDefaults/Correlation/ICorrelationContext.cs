namespace OneLoaf.ServiceDefaults.Correlation;

/// <summary>Correlation id of the current logical operation (an HTTP request or a consumed message).</summary>
public interface ICorrelationContext
{
    /// <summary>The current correlation id, or null outside of an operation.</summary>
    string? CorrelationId { get; }

    /// <summary>Sets the correlation id for the current asynchronous flow.</summary>
    void SetCorrelationId(string correlationId);
}
