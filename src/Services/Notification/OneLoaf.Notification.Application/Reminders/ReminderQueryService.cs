using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Notification.Application.Reminders;

/// <summary>The baker's reminders (calendar, debugging).</summary>
public sealed class ReminderQueryService(IReminderRepository reminders, ICurrentUser currentUser)
{
    /// <summary>Own reminders by due time.</summary>
    public Task<IReadOnlyList<ReminderDto>> ListAsync(ReminderListQuery query, CancellationToken cancellationToken) =>
        reminders.ListAsync(currentUser.Id, query, cancellationToken);
}
