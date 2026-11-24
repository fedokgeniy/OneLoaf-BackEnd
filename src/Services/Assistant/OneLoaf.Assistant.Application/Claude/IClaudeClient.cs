namespace OneLoaf.Assistant.Application.Claude;

/// <summary>
/// Claude Messages API. Implementations map transport failures (timeouts, 429, 5xx, network) to
/// <c>ExternalServiceUnavailableException</c>.
/// </summary>
public interface IClaudeClient
{
    /// <summary>Sends one request.</summary>
    Task<ClaudeResponse> SendAsync(ClaudeRequest request, CancellationToken cancellationToken);
}
