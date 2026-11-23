namespace OneLoaf.Notification.Application;

/// <summary>A message shown to the baker by a channel.</summary>
/// <param name="Title">Title of the notification.</param>
/// <param name="Body">Text.</param>
/// <param name="Url">Relative app URL opened on click.</param>
/// <param name="Tag">Groups notifications of the same object (a new one replaces the old one on the device).</param>
public sealed record NotificationMessage(string Title, string Body, string? Url, string Tag);

/// <summary>Who receives a notification.</summary>
/// <param name="UserId">User.</param>
/// <param name="Email">E-mail address (used only by the e-mail channel, never logged).</param>
/// <param name="TimeZone">IANA time zone of the kitchen, for times in the text.</param>
public sealed record Recipient(Guid UserId, string Email, string TimeZone);

/// <summary>Result of a delivery.</summary>
public enum DeliveryOutcome
{
    /// <summary>Delivered to at least one device or mailbox.</summary>
    Delivered,

    /// <summary>Nothing to deliver to (no push subscription, channel not configured).</summary>
    NoTarget,
}
