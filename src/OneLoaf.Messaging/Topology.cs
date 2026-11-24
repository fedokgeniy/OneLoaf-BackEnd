namespace OneLoaf.Messaging;

/// <summary>Routing keys of the topic exchange <see cref="Exchanges.Events"/>.</summary>
public static class RoutingKeys
{
    /// <summary><see cref="Contracts.ReminderDue"/>.</summary>
    public const string ReminderDue = "reminder.due";

    /// <summary><see cref="Contracts.BatchPlanned"/>.</summary>
    public const string BatchPlanned = "batch.planned";

    /// <summary><see cref="Contracts.BatchCompleted"/>.</summary>
    public const string BatchCompleted = "batch.completed";

    /// <summary><see cref="Contracts.StarterFed"/>.</summary>
    public const string StarterFed = "starter.fed";

    /// <summary><see cref="Contracts.AccountDeleted"/>.</summary>
    public const string AccountDeleted = "account.deleted";
}

/// <summary>Exchange names.</summary>
public static class Exchanges
{
    /// <summary>Topic exchange of all domain events.</summary>
    public const string Events = "oneloaf.events";

    /// <summary>Direct exchange of the delayed retry queues.</summary>
    public const string Retry = "oneloaf.retry";

    /// <summary>Fanout exchange of messages that failed all attempts.</summary>
    public const string DeadLetter = "oneloaf.dlx";
}

/// <summary>Queue names; each consuming service owns its queues.</summary>
public static class QueueNames
{
    /// <summary>Due reminders, consumed by the Notification dispatcher.</summary>
    public const string NotificationReminders = "notification.reminders";

    /// <summary>Batch events (planned, completed) for confirmation pushes.</summary>
    public const string NotificationBatchEvents = "notification.batch-events";

    /// <summary>Starter feedings for the "next feeding" push.</summary>
    public const string NotificationStarterEvents = "notification.starter-events";

    /// <summary>Messages that failed every attempt.</summary>
    public const string DeadLetter = "oneloaf.dead-letter";

    /// <summary>Delayed retry queue of a queue.</summary>
    public static string Retry(string queue) => queue + ".retry";
}

/// <summary>Message header names of the envelope.</summary>
public static class MessageHeaders
{
    /// <summary>Correlation id of the operation that caused the event.</summary>
    public const string CorrelationId = "correlation-id";

    /// <summary>W3C trace context.</summary>
    public const string TraceParent = "traceparent";

    /// <summary>Event type (routing key).</summary>
    public const string EventType = "event-type";

    /// <summary>When the change happened (ISO 8601).</summary>
    public const string OccurredAt = "occurred-at";

    /// <summary>Delivery attempt counter used by the retry policy.</summary>
    public const string Attempt = "x-attempt";
}
