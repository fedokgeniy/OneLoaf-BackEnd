using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OneLoaf.Domain;
using OneLoaf.Messaging;
using OneLoaf.Messaging.Contracts;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Notification.Application.Reminders;

/// <summary>A reminder that is due.</summary>
/// <param name="Id">Reminder.</param>
/// <param name="OwnerId">Owner.</param>
/// <param name="DueAt">Due time.</param>
public sealed record DueReminder(Guid Id, Guid OwnerId, DateTimeOffset DueAt);

/// <summary>A reminder as the dispatcher needs it.</summary>
/// <param name="Id">Reminder.</param>
/// <param name="OwnerId">Owner.</param>
/// <param name="Kind">Kind.</param>
/// <param name="Channel">Delivery channel.</param>
/// <param name="Status">Status.</param>
/// <param name="Message">Text written by the database when the reminder was scheduled.</param>
/// <param name="DueAt">Due time.</param>
/// <param name="BatchId">Batch of a kneading or stage reminder.</param>
/// <param name="StarterId">Starter of a feeding reminder.</param>
public sealed record ReminderDetails(
    Guid Id, Guid OwnerId, ReminderKind Kind, ReminderChannel Channel, ReminderStatus Status, string Message,
    DateTimeOffset DueAt, Guid? BatchId, Guid? StarterId);

/// <summary>A reminder in the baker's list.</summary>
/// <param name="Id">Reminder.</param>
/// <param name="Kind">Kind.</param>
/// <param name="Channel">Channel.</param>
/// <param name="Status">Status.</param>
/// <param name="Message">Text.</param>
/// <param name="DueAt">Due time.</param>
/// <param name="SentAt">Delivery time.</param>
/// <param name="Attempts">Delivery attempts so far.</param>
/// <param name="BatchId">Batch, if any.</param>
/// <param name="StarterId">Starter, if any.</param>
public sealed record ReminderDto(
    Guid Id, ReminderKind Kind, ReminderChannel Channel, ReminderStatus Status, string Message, DateTimeOffset DueAt,
    DateTimeOffset? SentAt, int Attempts, Guid? BatchId, Guid? StarterId);

/// <summary>Query of <c>GET /reminders</c>.</summary>
/// <param name="Status">Only this status.</param>
/// <param name="From">Due at or after.</param>
/// <param name="To">Due before.</param>
/// <param name="Limit">Most rows returned (1-200, default 50).</param>
public sealed record ReminderListQuery(ReminderStatus? Status = null, DateTimeOffset? From = null, DateTimeOffset? To = null, int? Limit = null)
{
    /// <summary>Largest page.</summary>
    public const int MaxLimit = 200;

    /// <summary>Default page.</summary>
    public const int DefaultLimit = 50;

    /// <summary>The limit clamped to 1-200.</summary>
    public int EffectiveLimit => Math.Clamp(Limit ?? DefaultLimit, 1, MaxLimit);
}

/// <summary>The <c>reminders</c> table.</summary>
public interface IReminderRepository
{
    /// <summary>
    /// Locks up to <paramref name="limit"/> due scheduled reminders (<c>FOR UPDATE SKIP LOCKED</c>, so parallel
    /// schedulers take different rows), calls <paramref name="process"/> for each inside the transaction and returns
    /// how many it accepted.
    /// </summary>
    Task<int> ProcessDueAsync(DateTimeOffset now, int limit, Func<DueReminder, CancellationToken, Task<bool>> process, CancellationToken cancellationToken);

    /// <summary>The reminder, or null.</summary>
    Task<ReminderDetails?> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Marks a scheduled reminder as sent and counts the attempt; false when it was no longer scheduled.</summary>
    Task<bool> MarkSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken);

    /// <summary>Counts a failed attempt; <paramref name="final"/> sets the status to failed.</summary>
    Task MarkAttemptFailedAsync(Guid id, bool final, CancellationToken cancellationToken);

    /// <summary>Reminders of the owner by due time.</summary>
    Task<IReadOnlyList<ReminderDto>> ListAsync(Guid ownerId, ReminderListQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// Marks reminders handed to the dispatcher, so the next scheduler run does not publish them again while delivery
/// (with retries) is in progress. A marker expires, so a reminder whose dispatcher died is published again.
/// </summary>
public interface IReminderInFlightMarker
{
    /// <summary>Sets the marker; false when it was already set.</summary>
    Task<bool> TryMarkAsync(Guid reminderId, TimeSpan lifetime, CancellationToken cancellationToken);
}

/// <summary>Counters of reminder delivery (Prometheus via OpenTelemetry).</summary>
public sealed class NotificationMetrics
{
    /// <summary>Meter name; registered by the service defaults.</summary>
    public const string MeterName = "OneLoaf";

    private readonly Counter<long> _sent;
    private readonly Counter<long> _failed;

    /// <summary>Creates the counters.</summary>
    public NotificationMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _sent = meter.CreateCounter<long>("oneloaf.reminders.sent", description: "Reminders delivered.");
        _failed = meter.CreateCounter<long>("oneloaf.reminders.failed", description: "Reminders that could not be delivered.");
    }

    /// <summary>A reminder was delivered.</summary>
    public void Sent(ReminderChannel channel) => _sent.Add(1, new KeyValuePair<string, object?>("channel", channel.ToString()));

    /// <summary>A reminder failed for good.</summary>
    public void Failed(ReminderChannel channel, string reason) =>
        _failed.Add(1, new KeyValuePair<string, object?>("channel", channel.ToString()), new KeyValuePair<string, object?>("reason", reason));
}

/// <summary>Reasons of failed reminders (metric label).</summary>
public static class FailureReasons
{
    /// <summary>No device or mailbox to deliver to.</summary>
    public const string NoTarget = "no_target";

    /// <summary>All delivery attempts failed.</summary>
    public const string Exhausted = "exhausted";
}

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

/// <summary>Texts of reminder notifications.</summary>
public static class ReminderTemplates
{
    /// <summary>Notification of a reminder; the body is the text the database stored with the reminder.</summary>
    public static NotificationMessage For(ReminderDetails reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        var title = reminder.Kind switch
        {
            ReminderKind.StarterFeeding => "Time to feed your starter",
            ReminderKind.BeforeKneading => "Kneading starts soon",
            _ => "Next stage",
        };
        var url = reminder.BatchId is { } batchId ? $"/batches/{batchId}"
            : reminder.StarterId is { } starterId ? $"/starters/{starterId}"
            : null;
        return new NotificationMessage(title, reminder.Message, url, $"reminder-{reminder.Id:N}");
    }
}

/// <summary>The baker's reminders (calendar, debugging).</summary>
public sealed class ReminderQueryService(IReminderRepository reminders, ICurrentUser currentUser)
{
    /// <summary>Own reminders by due time.</summary>
    public Task<IReadOnlyList<ReminderDto>> ListAsync(ReminderListQuery query, CancellationToken cancellationToken) =>
        reminders.ListAsync(currentUser.Id, query, cancellationToken);
}
