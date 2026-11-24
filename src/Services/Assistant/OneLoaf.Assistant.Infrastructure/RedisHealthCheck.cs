using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>Readiness check: Redis answers PING.</summary>
public sealed class RedisHealthCheck(IConnectionMultiplexer redis) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (RedisException ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
