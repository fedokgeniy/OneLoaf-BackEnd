using System.ComponentModel.DataAnnotations;

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
