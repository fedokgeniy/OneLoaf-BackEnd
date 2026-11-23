using System.ComponentModel.DataAnnotations;
using OneLoaf.Domain;

namespace OneLoaf.Notification.Application;

/// <summary>Notification settings (<c>Notification__VapidPublicKey</c>, <c>Notification__SchedulerIntervalSeconds</c>, ...).</summary>
public sealed class NotificationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notification";

    /// <summary>VAPID subject (<c>mailto:</c> or https URL of the operator).</summary>
    public string VapidSubject { get; set; } = string.Empty;

    /// <summary>VAPID public key (base64url), given to the browser for <c>PushManager.subscribe</c>.</summary>
    public string VapidPublicKey { get; set; } = string.Empty;

    /// <summary>VAPID private key (base64url); kept only in the environment.</summary>
    public string VapidPrivateKey { get; set; } = string.Empty;

    /// <summary>How often the scheduler looks for due reminders.</summary>
    [Range(1, 3600)]
    public int SchedulerIntervalSeconds { get; set; } = 30;

    /// <summary>Most reminders published per scheduler run.</summary>
    [Range(1, 1000)]
    public int SchedulerBatchSize { get; set; } = 100;

    /// <summary>Web Push is configured (both VAPID keys are set).</summary>
    public bool PushConfigured => VapidPublicKey.Length > 0 && VapidPrivateKey.Length > 0;
}

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

/// <summary>Recipients by user id.</summary>
public interface IRecipientDirectory
{
    /// <summary>The recipient, or null when the account no longer exists.</summary>
    Task<Recipient?> GetAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>Formats times in the baker's time zone.</summary>
public static class LocalTime
{
    private const string Pattern = "HH:mm, d MMM";

    /// <summary>The instant in the time zone (UTC when the zone is unknown), e.g. <c>06:30, 14 Dec</c>.</summary>
    public static string Format(DateTimeOffset instant, string timeZone)
    {
        var local = TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone)
            ? TimeZoneInfo.ConvertTime(instant, zone)
            : instant.ToUniversalTime();
        return local.ToString(Pattern, System.Globalization.CultureInfo.InvariantCulture);
    }
}
