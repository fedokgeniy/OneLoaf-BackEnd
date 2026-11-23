using Microsoft.Extensions.Logging;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;

namespace OneLoaf.Notification.Application.Reminders;

/// <summary>Publishes <c>reminder.due</c> for due reminders (one scheduler run).</summary>
public sealed partial class ReminderScheduler(
    IReminderRepository reminders,
    IReminderInFlightMarker inFlight,
    IEventPublisher publisher,
    TimeProvider timeProvider,
    NotificationOptions options,
    ILogger<ReminderScheduler> logger)
{
    /// <summary>How long a published reminder is not published again (covers all retry delays).</summary>
    public static readonly TimeSpan InFlightLifetime = TimeSpan.FromMinutes(5);

    /// <summary>Publishes the due reminders; returns how many were published.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var published = await reminders.ProcessDueAsync(now, options.SchedulerBatchSize, async (reminder, ct) =>
        {
            if (!await inFlight.TryMarkAsync(reminder.Id, InFlightLifetime, ct))
            {
                return false;
            }

            await publisher.PublishAsync(new ReminderDue(reminder.Id, reminder.OwnerId, reminder.DueAt, now), ct);
            return true;
        }, cancellationToken);

        if (published > 0)
        {
            LogPublished(logger, published);
        }

        return published;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Published {Count} due reminders")]
    private static partial void LogPublished(ILogger logger, int count);
}
