using System.ComponentModel.DataAnnotations;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Assistant.Application.Budget;

/// <summary>Prices per million tokens used to compute the cost of a turn.</summary>
public sealed class PricingOptions
{
    /// <summary>Input tokens.</summary>
    [Range(0, 1000)]
    public decimal InputPerMTok { get; set; } = 1.00m;

    /// <summary>Output tokens.</summary>
    [Range(0, 1000)]
    public decimal OutputPerMTok { get; set; } = 5.00m;

    /// <summary>Input tokens read from the cache.</summary>
    [Range(0, 1000)]
    public decimal CacheReadPerMTok { get; set; } = 0.10m;
}

/// <summary>Limits and loop settings of the assistant (<c>Assistant__*</c>).</summary>
public sealed class AssistantOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Assistant";

    /// <summary>Messages per user per day.</summary>
    [Range(1, 100_000)]
    public int DailyRequestLimit { get; set; } = 50;

    /// <summary>Tokens per user per day.</summary>
    [Range(1, 100_000_000)]
    public int DailyTokenLimit { get; set; } = 200_000;

    /// <summary>Global spend per calendar month, USD; requests stop when it is reached.</summary>
    [Range(0, 100_000)]
    public decimal MonthlyBudgetUsd { get; set; } = 4.5m;

    /// <summary>Tool rounds per message.</summary>
    [Range(1, 20)]
    public int MaxToolRounds { get; set; } = 6;

    /// <summary>Previous messages sent as context.</summary>
    [Range(0, 200)]
    public int HistoryWindow { get; set; } = 20;

    /// <summary>Messages per user per minute.</summary>
    [Range(1, 1000)]
    public int MessagesPerMinute { get; set; } = 10;

    /// <summary>Token prices.</summary>
    [Required]
    public PricingOptions Pricing { get; set; } = new();
}

/// <summary>Usage counters of a user for one day.</summary>
/// <param name="Requests">Messages sent.</param>
/// <param name="Tokens">Input + output tokens.</param>
/// <param name="CostUsd">Cost in USD.</param>
public sealed record DailyUsage(int Requests, long Tokens, decimal CostUsd);

/// <summary>Usage counters (Redis).</summary>
public interface IUsageStore
{
    /// <summary>Counters of a user for a day.</summary>
    Task<DailyUsage> GetDailyAsync(Guid userId, DateOnly day, CancellationToken cancellationToken);

    /// <summary>Global cost of a month in USD.</summary>
    Task<decimal> GetMonthlyCostAsync(DateOnly month, CancellationToken cancellationToken);

    /// <summary>Adds one request with its tokens and cost to the user's day and to the month.</summary>
    Task AddAsync(Guid userId, DateOnly day, long tokens, decimal costUsd, CancellationToken cancellationToken);
}

/// <summary>Today's usage of the current user against the limits.</summary>
/// <param name="Day">Day (UTC).</param>
/// <param name="Requests">Messages sent today.</param>
/// <param name="RequestLimit">Messages per day.</param>
/// <param name="Tokens">Tokens used today.</param>
/// <param name="TokenLimit">Tokens per day.</param>
/// <param name="CostUsd">Cost today.</param>
public sealed record UsageDto(DateOnly Day, int Requests, int RequestLimit, long Tokens, int TokenLimit, decimal CostUsd);

/// <summary>Keeps the assistant within the per-user and global budget.</summary>
public sealed class UsageGuard(IUsageStore usage, AssistantOptions options, TimeProvider timeProvider, ICurrentUser currentUser)
{
    /// <summary>Message when the user's daily limit is reached.</summary>
    public const string DailyLimitMessage = "The daily assistant limit is reached. The calculator and forms keep working.";

    /// <summary>Message when the global budget is exhausted.</summary>
    public const string BudgetMessage = "The assistant budget for this month is used up. The calculator and forms keep working.";

    private const decimal TokensPerMillion = 1_000_000m;
    private const int CostDigits = 6;

    /// <summary>Throws 429 when the user's daily limits or the monthly budget are reached.</summary>
    public async Task EnsureAllowedAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var daily = await usage.GetDailyAsync(currentUser.Id, today, cancellationToken);
        if (daily.Requests >= options.DailyRequestLimit || daily.Tokens >= options.DailyTokenLimit)
        {
            throw new TooManyRequestsException(DailyLimitMessage, now.Date.AddDays(1) - now.UtcDateTime);
        }

        var month = new DateOnly(today.Year, today.Month, 1);
        if (await usage.GetMonthlyCostAsync(month, cancellationToken) >= options.MonthlyBudgetUsd)
        {
            throw new TooManyRequestsException(BudgetMessage, month.AddMonths(1).ToDateTime(TimeOnly.MinValue) - now.UtcDateTime);
        }
    }

    /// <summary>Cost of tokens by the configured prices.</summary>
    public decimal Cost(int inputTokens, int outputTokens, int cacheReadTokens) =>
        Math.Round((inputTokens * options.Pricing.InputPerMTok + outputTokens * options.Pricing.OutputPerMTok
                    + cacheReadTokens * options.Pricing.CacheReadPerMTok) / TokensPerMillion, CostDigits);

    /// <summary>Records a finished turn.</summary>
    public Task RecordAsync(int inputTokens, int outputTokens, decimal costUsd, CancellationToken cancellationToken) =>
        usage.AddAsync(currentUser.Id, DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime), (long)inputTokens + outputTokens,
            costUsd, cancellationToken);

    /// <summary>Today's usage of the current user.</summary>
    public async Task<UsageDto> GetTodayAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var daily = await usage.GetDailyAsync(currentUser.Id, today, cancellationToken);
        return new UsageDto(today, daily.Requests, options.DailyRequestLimit, daily.Tokens, options.DailyTokenLimit, daily.CostUsd);
    }
}
