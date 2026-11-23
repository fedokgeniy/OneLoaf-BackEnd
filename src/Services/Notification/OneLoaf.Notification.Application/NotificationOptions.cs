using System.ComponentModel.DataAnnotations;

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
