namespace OneLoaf.Notification.Application.Reminders;

/// <summary>
/// Marks reminders handed to the dispatcher, so the next scheduler run does not publish them again while delivery
/// (with retries) is in progress. A marker expires, so a reminder whose dispatcher died is published again.
/// </summary>
public interface IReminderInFlightMarker
{
    /// <summary>Sets the marker; false when it was already set.</summary>
    Task<bool> TryMarkAsync(Guid reminderId, TimeSpan lifetime, CancellationToken cancellationToken);
}
