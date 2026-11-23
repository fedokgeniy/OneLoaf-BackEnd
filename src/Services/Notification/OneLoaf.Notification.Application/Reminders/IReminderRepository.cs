namespace OneLoaf.Notification.Application.Reminders;

/// <summary>The <c>reminders</c> table.</summary>
public interface IReminderRepository
{
    /// <summary>
    /// Locks up to <paramref name="limit"/> due scheduled reminders (<c>FOR UPDATE SKIP LOCKED</c>, so parallel
    /// schedulers take different rows), calls <paramref name="process"/> for each inside the transaction and returns
    /// how many it accepted.
    /// </summary>
    Task<int> ProcessDueAsync(DateTimeOffset now, int limit, Func<DueReminder, CancellationToken, Task<bool>> process, CancellationToken cancellationToken);

    /// <summary>The reminder, or null.</summary>
    Task<ReminderDetails?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marks a scheduled reminder as sent and counts the attempt; false when it was no longer scheduled.</summary>
    Task<bool> MarkSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken);

    /// <summary>Counts a failed attempt; <paramref name="final"/> sets the status to failed.</summary>
    Task MarkAttemptFailedAsync(Guid id, bool final, CancellationToken cancellationToken);

    /// <summary>Reminders of the owner by due time.</summary>
    Task<IReadOnlyList<ReminderDto>> ListAsync(Guid ownerId, ReminderListQuery query, CancellationToken cancellationToken);
}
