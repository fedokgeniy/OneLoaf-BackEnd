namespace OneLoaf.Assistant.Application.Budget;

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
