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

/// <summary>Delivery details of a consumed message.</summary>
/// <param name="MessageId">Id set by the publisher (stable across retries).</param>
/// <param name="Attempt">Delivery attempt, starting at 1.</param>
/// <param name="MaxAttempts">Attempts before the message goes to the dead-letter queue.</param>
/// <param name="CorrelationId">Correlation id of the operation that published the event.</param>
public sealed record MessageContext(string MessageId, int Attempt, int MaxAttempts, string? CorrelationId)
{
    /// <summary>This is the last attempt; a failure now dead-letters the message.</summary>
    public bool IsLastAttempt => Attempt >= MaxAttempts;
}

/// <summary>
/// Handles one event type. Throwing schedules a delayed retry; after the last attempt the message is dead-lettered.
/// Handlers must be idempotent: a message can be delivered more than once.
/// </summary>
/// <typeparam name="TEvent">Event type.</typeparam>
public interface IIntegrationEventConsumer<in TEvent>
    where TEvent : IntegrationEvent
{
    /// <summary>Handles the event.</summary>
    Task HandleAsync(TEvent integrationEvent, MessageContext context, CancellationToken cancellationToken);
}
