namespace OneLoaf.Web.Shared.Contracts.Notification;

/// <summary>
/// Reminder of the Notification service (<c>GET /reminders</c>, US-29). <see cref="Kind"/>: starter_feeding,
/// before_kneading, stage_start; <see cref="Status"/>: scheduled, sent, cancelled, failed.
/// </summary>
public sealed record ReminderDto(
    Guid Id,
    string Kind,
    string Channel,
    string Status,
    string Message,
    DateTimeOffset DueAt,
    DateTimeOffset? SentAt,
    int Attempts,
    Guid? BatchId,
    Guid? StarterId);

/// <summary><c>GET /push/vapid-public-key</c>.</summary>
public sealed record VapidPublicKeyResponse(string PublicKey);

/// <summary>Browser push subscription keys.</summary>
public sealed record PushKeys(string P256dh, string Auth);

/// <summary><c>POST /push/subscriptions</c> body.</summary>
public sealed record SubscribeRequest(string Endpoint, PushKeys Keys);

/// <summary>Stored push subscription.</summary>
public sealed record PushSubscriptionDto(Guid Id, string Endpoint, DateTimeOffset CreatedAt);

/// <summary><c>POST /push/test</c> response.</summary>
public sealed record TestPushResponse(bool Delivered);
