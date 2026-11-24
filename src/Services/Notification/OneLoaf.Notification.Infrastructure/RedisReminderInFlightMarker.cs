using OneLoaf.Notification.Application.Reminders;
using StackExchange.Redis;

namespace OneLoaf.Notification.Infrastructure;

/// <summary><c>SET reminder:inflight:{id} NX EX</c>.</summary>
public sealed class RedisReminderInFlightMarker(IConnectionMultiplexer redis) : IReminderInFlightMarker
{
    private const string MarkerValue = "1";

    /// <inheritdoc />
    public Task<bool> TryMarkAsync(Guid reminderId, TimeSpan lifetime, CancellationToken cancellationToken) =>
        redis.GetDatabase().StringSetAsync(CacheKeys.ReminderInFlight(reminderId), MarkerValue, lifetime, When.NotExists);
}
