namespace OneLoaf.Notification.Infrastructure;

/// <summary>Cache keys used by the Notification service.</summary>
public static class CacheKeys
{
    /// <summary>Marker of a reminder handed to the dispatcher.</summary>
    public static string ReminderInFlight(Guid reminderId) => $"reminder:inflight:{reminderId:N}";
}
