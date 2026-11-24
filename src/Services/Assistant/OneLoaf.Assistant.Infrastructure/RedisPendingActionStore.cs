using System.Text.Json;
using OneLoaf.Assistant.Application.Chat;
using StackExchange.Redis;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>Pending proposals in Redis with expiry.</summary>
public sealed class RedisPendingActionStore(IConnectionMultiplexer redis) : IPendingActionStore
{
    /// <inheritdoc />
    public Task SaveAsync(PendingAction action, TimeSpan lifetime, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);
        return redis.GetDatabase().StringSetAsync(CacheKeys.PendingAction(action.Id), JsonSerializer.Serialize(action), lifetime);
    }

    /// <inheritdoc />
    public async Task<PendingAction?> GetAsync(Guid actionId, CancellationToken cancellationToken)
    {
        var value = await redis.GetDatabase().StringGetAsync(CacheKeys.PendingAction(actionId));
        return value.IsNullOrEmpty ? null : JsonSerializer.Deserialize<PendingAction>(value.ToString());
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid actionId, CancellationToken cancellationToken) =>
        redis.GetDatabase().KeyDeleteAsync(CacheKeys.PendingAction(actionId));
}
