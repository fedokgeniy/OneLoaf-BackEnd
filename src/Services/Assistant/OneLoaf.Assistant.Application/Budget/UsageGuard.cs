using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Assistant.Application.Budget;

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
