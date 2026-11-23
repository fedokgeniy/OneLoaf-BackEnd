using OneLoaf.Domain;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;

namespace OneLoaf.Notification.Application.Events;

/// <summary>
/// Informational pushes for domain events (batch planned, batch completed, starter fed). A baker without devices
/// simply gets nothing; delivery failures are retried by the broker.
/// </summary>
public sealed class EventNotifier(IRecipientDirectory recipients, IEnumerable<INotificationChannel> channels)
{
    /// <summary>Pushes the message built for the recipient.</summary>
    public async Task PushAsync(Guid userId, Func<Recipient, NotificationMessage> build, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(build);
        var recipient = await recipients.GetAsync(userId, cancellationToken);
        if (recipient is null)
        {
            return;
        }

        var push = channels.First(c => c.Channel == ReminderChannel.Push);
        await push.SendAsync(recipient, build(recipient), cancellationToken);
    }
}

/// <summary>Texts of event notifications.</summary>
public static class EventTemplates
{
    /// <summary>"Batch planned" with the first reminder time.</summary>
    public static NotificationMessage BatchPlanned(BatchPlanned e, Recipient recipient)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(recipient);
        var body = e.FirstReminderAt is { } first
            ? $"\"{e.Name}\" is planned. First reminder at {LocalTime.Format(first, recipient.TimeZone)}."
            : $"\"{e.Name}\" is planned.";
        return new NotificationMessage("Batch planned", body, $"/batches/{e.BatchId}", $"batch-{e.BatchId:N}");
    }

    /// <summary>"Rate your bake" after completion.</summary>
    public static NotificationMessage BatchCompleted(BatchCompleted e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var url = e.BakeRecordId is { } record ? $"/history/{record}" : $"/batches/{e.BatchId}";
        return new NotificationMessage("Rate your bake", "How did it turn out? Rate the bake and note what to change next time.", url,
            $"batch-{e.BatchId:N}");
    }

    /// <summary>"Next feeding at ..." after a feeding.</summary>
    public static NotificationMessage StarterFed(StarterFed e, Recipient recipient)
    {
        ArgumentNullException.ThrowIfNull(e);
        ArgumentNullException.ThrowIfNull(recipient);
        var body = e.NextFeedingAt is { } next
            ? $"Feeding recorded. Next feeding at {LocalTime.Format(next, recipient.TimeZone)}."
            : "Feeding recorded.";
        return new NotificationMessage("Starter fed", body, $"/starters/{e.StarterId}", $"starter-{e.StarterId:N}");
    }
}

/// <summary>Push after a batch was planned.</summary>
public sealed class BatchPlannedConsumer(EventNotifier notifier) : IIntegrationEventConsumer<BatchPlanned>
{
    /// <inheritdoc />
    public Task HandleAsync(BatchPlanned integrationEvent, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return notifier.PushAsync(integrationEvent.OwnerId, r => EventTemplates.BatchPlanned(integrationEvent, r), cancellationToken);
    }
}

/// <summary>Push after a batch was completed.</summary>
public sealed class BatchCompletedConsumer(EventNotifier notifier) : IIntegrationEventConsumer<BatchCompleted>
{
    /// <inheritdoc />
    public Task HandleAsync(BatchCompleted integrationEvent, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return notifier.PushAsync(integrationEvent.OwnerId, _ => EventTemplates.BatchCompleted(integrationEvent), cancellationToken);
    }
}

/// <summary>Push after a starter feeding.</summary>
public sealed class StarterFedConsumer(EventNotifier notifier) : IIntegrationEventConsumer<StarterFed>
{
    /// <inheritdoc />
    public Task HandleAsync(StarterFed integrationEvent, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        return notifier.PushAsync(integrationEvent.OwnerId, r => EventTemplates.StarterFed(integrationEvent, r), cancellationToken);
    }
}
