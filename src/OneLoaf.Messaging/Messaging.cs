using OneLoaf.Messaging.Contracts;

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

/// <summary>Publishes domain events after the change is committed.</summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes the event. Delivery is best effort: the database stays the source of truth (reminders are polled), so
    /// a broker outage is logged and does not fail the user's request.
    /// </summary>
    Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
