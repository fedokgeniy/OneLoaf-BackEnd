using OneLoaf.Domain;

namespace OneLoaf.Notification.Application;

/// <summary>
/// A delivery channel. Transient failures (push service or SMTP unavailable) are thrown so the message is retried;
/// a missing target is reported as <see cref="DeliveryOutcome.NoTarget"/>.
/// </summary>
public interface INotificationChannel
{
    /// <summary>Channel value stored in <c>reminders.channel</c>.</summary>
    ReminderChannel Channel { get; }

    /// <summary>Delivers the message.</summary>
    Task<DeliveryOutcome> SendAsync(Recipient recipient, NotificationMessage message, CancellationToken cancellationToken);
}
