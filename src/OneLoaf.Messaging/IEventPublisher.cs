using OneLoaf.Messaging.Contracts;

namespace OneLoaf.Messaging;

/// <summary>Publishes domain events after the change is committed.</summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes the event. Delivery is best effort: the database stays the source of truth (reminders are polled), so
    /// a broker outage is logged and does not fail the user's request.
    /// </summary>
    Task PublishAsync(IntegrationEvent integrationEvent, CancellationToken cancellationToken);
}
