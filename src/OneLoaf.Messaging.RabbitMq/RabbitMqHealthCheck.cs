using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OneLoaf.Messaging.RabbitMq;

/// <summary>Readiness check: the broker connection can be opened.</summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnection connection) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var open = await connection.GetAsync(cancellationToken);
            return open.IsOpen ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
