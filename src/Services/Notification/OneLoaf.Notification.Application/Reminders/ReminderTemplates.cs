using OneLoaf.Domain;

namespace OneLoaf.Notification.Application.Reminders;

/// <summary>Texts of reminder notifications.</summary>
public static class ReminderTemplates
{
    /// <summary>Notification of a reminder; the body is the text the database stored with the reminder.</summary>
    public static NotificationMessage For(ReminderDetails reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        var title = reminder.Kind switch
        {
            ReminderKind.StarterFeeding => "Time to feed your starter",
            ReminderKind.BeforeKneading => "Feed your starter for the batch",
            _ => "Next stage",
        };
        var url = reminder.BatchId is { } batchId ? $"/batches/{batchId}"
            : reminder.StarterId is { } starterId ? $"/starters/{starterId}"
            : null;
        return new NotificationMessage(title, reminder.Message, url, $"reminder-{reminder.Id:N}");
    }
}
