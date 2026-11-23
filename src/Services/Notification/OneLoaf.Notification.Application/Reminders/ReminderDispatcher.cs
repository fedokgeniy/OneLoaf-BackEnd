using Microsoft.Extensions.Logging;
using OneLoaf.Domain;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;

namespace OneLoaf.Notification.Application.Reminders;

/// <summary>
/// Delivers a due reminder (US-29). Idempotent: continues only while the reminder is still scheduled. A failed
/// delivery counts an attempt and is retried by the broker; after the last attempt the reminder is marked failed and
/// the message is dead-lettered.
/// </summary>
public sealed partial class ReminderDispatcher(
    IReminderRepository reminders,
    IRecipientDirectory recipients,
    IEnumerable<INotificationChannel> channels,
    NotificationMetrics metrics,
    TimeProvider timeProvider,
    ILogger<ReminderDispatcher> logger) : IIntegrationEventConsumer<ReminderDue>
{
    /// <inheritdoc />
    public async Task HandleAsync(ReminderDue integrationEvent, MessageContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        ArgumentNullException.ThrowIfNull(context);
        var reminder = await reminders.GetAsync(integrationEvent.ReminderId, cancellationToken);
        if (reminder is not { Status: ReminderStatus.Scheduled })
        {
            return;
        }

        var recipient = await recipients.GetAsync(reminder.OwnerId, cancellationToken);
        var channel = channels.FirstOrDefault(c => c.Channel == reminder.Channel);
        if (recipient is null || channel is null)
        {
            await FailAsync(reminder, FailureReasons.NoTarget, cancellationToken);
            return;
        }

        DeliveryOutcome outcome;
        try
        {
            outcome = await channel.SendAsync(recipient, ReminderTemplates.For(reminder), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            if (!context.IsLastAttempt)
            {
                await reminders.MarkAttemptFailedAsync(reminder.Id, final: false, cancellationToken);
                throw;
            }

            // Marked failed, then rethrown so the broker moves the message to the dead-letter queue for inspection.
            LogExhausted(logger, ex, reminder.Id, context.Attempt);
            await FailAsync(reminder, FailureReasons.Exhausted, cancellationToken);
            throw;
        }

        if (outcome == DeliveryOutcome.NoTarget)
        {
            await FailAsync(reminder, FailureReasons.NoTarget, cancellationToken);
            return;
        }

        if (await reminders.MarkSentAsync(reminder.Id, timeProvider.GetUtcNow(), cancellationToken))
        {
            metrics.Sent(reminder.Channel);
            LogSent(logger, reminder.Id, reminder.Channel);
        }
    }

    private async Task FailAsync(ReminderDetails reminder, string reason, CancellationToken cancellationToken)
    {
        await reminders.MarkAttemptFailedAsync(reminder.Id, final: true, cancellationToken);
        metrics.Failed(reminder.Channel, reason);
        LogFailed(logger, reminder.Id, reason);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Reminder {ReminderId} sent by {Channel}")]
    private static partial void LogSent(ILogger logger, Guid reminderId, ReminderChannel channel);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reminder {ReminderId} failed: {Reason}")]
    private static partial void LogFailed(ILogger logger, Guid reminderId, string reason);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reminder {ReminderId} could not be delivered after {Attempts} attempts")]
    private static partial void LogExhausted(ILogger logger, Exception exception, Guid reminderId, int attempts);
}
