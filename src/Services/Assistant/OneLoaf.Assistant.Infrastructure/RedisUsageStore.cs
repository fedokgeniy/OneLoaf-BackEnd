using System.Globalization;
using OneLoaf.Assistant.Application.Budget;
using StackExchange.Redis;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>Usage counters in Redis (daily per user, monthly global), stored as micro-dollars.</summary>
public sealed class RedisUsageStore(IConnectionMultiplexer redis, AssistantMetrics metrics) : IUsageStore
{
    private const string Requests = "requests";
    private const string Tokens = "tokens";
    private const string CostMicro = "cost_micro";
    private const decimal Micro = 1_000_000m;
    private static readonly TimeSpan DailyTtl = TimeSpan.FromHours(48);
    private static readonly TimeSpan MonthlyTtl = TimeSpan.FromDays(40);

    /// <inheritdoc />
    public async Task<DailyUsage> GetDailyAsync(Guid userId, DateOnly day, CancellationToken cancellationToken)
    {
        var values = await redis.GetDatabase().HashGetAsync(CacheKeys.DailyUsage(userId, day), [Requests, Tokens, CostMicro]);
        return new DailyUsage((int)ToLong(values[0]), ToLong(values[1]), ToLong(values[2]) / Micro);
    }

    /// <inheritdoc />
    public async Task<decimal> GetMonthlyCostAsync(DateOnly month, CancellationToken cancellationToken) =>
        ToLong(await redis.GetDatabase().StringGetAsync(CacheKeys.MonthlyCost(month))) / Micro;

    /// <inheritdoc />
    public async Task AddAsync(Guid userId, DateOnly day, long tokens, decimal costUsd, CancellationToken cancellationToken)
    {
        var db = redis.GetDatabase();
        var key = CacheKeys.DailyUsage(userId, day);
        var micro = (long)Math.Round(costUsd * Micro);
        metrics.Turn(tokens, costUsd);
        await db.HashIncrementAsync(key, Requests);
        await db.HashIncrementAsync(key, Tokens, tokens);
        await db.HashIncrementAsync(key, CostMicro, micro);
        await db.KeyExpireAsync(key, DailyTtl);
        var monthKey = CacheKeys.MonthlyCost(new DateOnly(day.Year, day.Month, 1));
        await db.StringIncrementAsync(monthKey, micro);
        await db.KeyExpireAsync(monthKey, MonthlyTtl);
    }

    private static long ToLong(RedisValue value) =>
        value.IsNullOrEmpty ? 0 : long.Parse(value.ToString(), CultureInfo.InvariantCulture);
}
