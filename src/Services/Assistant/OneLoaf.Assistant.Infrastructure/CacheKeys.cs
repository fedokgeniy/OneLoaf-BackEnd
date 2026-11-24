namespace OneLoaf.Assistant.Infrastructure;

/// <summary>Cache keys used by the assistant.</summary>
public static class CacheKeys
{
    /// <summary>A pending proposal.</summary>
    public static string PendingAction(Guid actionId) => $"ai:pending:{actionId:N}";

    /// <summary>Daily usage counters of a user.</summary>
    public static string DailyUsage(Guid userId, DateOnly day) => $"ai:usage:{userId:N}:{day:yyyyMMdd}";

    /// <summary>Global monthly cost.</summary>
    public static string MonthlyCost(DateOnly month) => $"ai:cost:{month:yyyyMM}";
}
