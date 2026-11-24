using OneLoaf.Messaging.Contracts;

namespace OneLoaf.Messaging;

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
