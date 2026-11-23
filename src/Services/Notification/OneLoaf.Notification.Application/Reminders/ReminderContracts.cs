using OneLoaf.Domain;

namespace OneLoaf.Notification.Application.Reminders;

/// <summary>A reminder that is due.</summary>
/// <param name="Id">Reminder.</param>
/// <param name="OwnerId">Owner.</param>
/// <param name="DueAt">Due time.</param>
public sealed record DueReminder(Guid Id, Guid OwnerId, DateTimeOffset DueAt);

/// <summary>A reminder as the dispatcher needs it.</summary>
/// <param name="Id">Reminder.</param>
/// <param name="OwnerId">Owner.</param>
/// <param name="Kind">Kind.</param>
/// <param name="Channel">Delivery channel.</param>
/// <param name="Status">Status.</param>
/// <param name="Message">Text written by the database when the reminder was scheduled.</param>
/// <param name="DueAt">Due time.</param>
/// <param name="BatchId">Batch of a kneading or stage reminder.</param>
/// <param name="StarterId">Starter of a feeding reminder.</param>
public sealed record ReminderDetails(
    Guid Id, Guid OwnerId, ReminderKind Kind, ReminderChannel Channel, ReminderStatus Status, string Message,
    DateTimeOffset DueAt, Guid? BatchId, Guid? StarterId);

/// <summary>A reminder in the baker's list.</summary>
/// <param name="Id">Reminder.</param>
/// <param name="Kind">Kind.</param>
/// <param name="Channel">Channel.</param>
/// <param name="Status">Status.</param>
/// <param name="Message">Text.</param>
/// <param name="DueAt">Due time.</param>
/// <param name="SentAt">Delivery time.</param>
/// <param name="Attempts">Delivery attempts so far.</param>
/// <param name="BatchId">Batch, if any.</param>
/// <param name="StarterId">Starter, if any.</param>
public sealed record ReminderDto(
    Guid Id, ReminderKind Kind, ReminderChannel Channel, ReminderStatus Status, string Message, DateTimeOffset DueAt,
    DateTimeOffset? SentAt, int Attempts, Guid? BatchId, Guid? StarterId);

/// <summary>Query of <c>GET /reminders</c>.</summary>
/// <param name="Status">Only this status: scheduled, sent, cancelled or failed.</param>
/// <param name="From">Due at or after.</param>
/// <param name="To">Due before.</param>
/// <param name="Limit">Most rows returned (1-200, default 50).</param>
public sealed record ReminderListQuery(string? Status = null, DateTimeOffset? From = null, DateTimeOffset? To = null, int? Limit = null)
{
    /// <summary>Largest page.</summary>
    public const int MaxLimit = 200;

    /// <summary>Default page.</summary>
    public const int DefaultLimit = 50;

    /// <summary>The limit clamped to 1-200.</summary>
    public int EffectiveLimit => Math.Clamp(Limit ?? DefaultLimit, 1, MaxLimit);
}

/// <summary>Reasons of failed reminders (metric label).</summary>
public static class FailureReasons
{
    /// <summary>No device or mailbox to deliver to.</summary>
    public const string NoTarget = "no_target";

    /// <summary>All delivery attempts failed.</summary>
    public const string Exhausted = "exhausted";
}
