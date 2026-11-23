using Microsoft.EntityFrameworkCore;
using OneLoaf.Domain;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.Notification.Application;
using OneLoaf.Notification.Application.Push;
using OneLoaf.Notification.Application.Reminders;

namespace OneLoaf.Notification.Infrastructure;

/// <summary>The <c>reminders</c> table.</summary>
public sealed class ReminderRepository(OneLoafDbContext db) : IReminderRepository
{
    /// <inheritdoc />
    public async Task<int> ProcessDueAsync(
        DateTimeOffset now, int limit, Func<DueReminder, CancellationToken, Task<bool>> process, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(process);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var due = await db.Database.SqlQuery<DueRow>($"""
            SELECT id AS "Id", owner_id AS "OwnerId", due_at AS "DueAt"
            FROM reminders
            WHERE status = 'scheduled' AND due_at <= {now}
            ORDER BY due_at
            LIMIT {limit}
            FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);

        var accepted = 0;
        foreach (var row in due)
        {
            if (await process(new DueReminder(row.Id, row.OwnerId, row.DueAt), cancellationToken))
            {
                accepted++;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return accepted;
    }

    /// <inheritdoc />
    public Task<ReminderDetails?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        db.Reminders.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ReminderDetails(
                r.Id, r.OwnerId, r.Kind, r.Channel, r.Status, r.Message, new DateTimeOffset(r.DueAt, TimeSpan.Zero),
                r.BatchItem != null ? r.BatchItem.BatchId : r.BatchStage != null ? r.BatchStage.BatchItem.BatchId : null,
                r.StarterId))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> MarkSentAsync(Guid id, DateTimeOffset sentAt, CancellationToken cancellationToken) =>
        await db.Reminders
            .Where(r => r.Id == id && r.Status == ReminderStatus.Scheduled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ReminderStatus.Sent)
                .SetProperty(r => r.SentAt, sentAt.UtcDateTime)
                .SetProperty(r => r.Attempts, r => r.Attempts + 1), cancellationToken) > 0;

    /// <inheritdoc />
    public Task MarkAttemptFailedAsync(Guid id, bool final, CancellationToken cancellationToken) =>
        db.Reminders
            .Where(r => r.Id == id && r.Status == ReminderStatus.Scheduled)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, final ? ReminderStatus.Failed : ReminderStatus.Scheduled)
                .SetProperty(r => r.Attempts, r => r.Attempts + 1), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReminderDto>> ListAsync(Guid ownerId, ReminderListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var reminders = db.Reminders.AsNoTracking().Where(r => r.OwnerId == ownerId);
        if (query.Status is { } status)
        {
            reminders = reminders.Where(r => r.Status == status);
        }

        if (query.From is { } from)
        {
            reminders = reminders.Where(r => r.DueAt >= from.UtcDateTime);
        }

        if (query.To is { } to)
        {
            reminders = reminders.Where(r => r.DueAt < to.UtcDateTime);
        }

        return await reminders
            .OrderBy(r => r.DueAt)
            .Take(query.EffectiveLimit)
            .Select(r => new ReminderDto(
                r.Id, r.Kind, r.Channel, r.Status, r.Message, new DateTimeOffset(r.DueAt, TimeSpan.Zero),
                r.SentAt == null ? null : new DateTimeOffset(r.SentAt.Value, TimeSpan.Zero), r.Attempts,
                r.BatchItem != null ? r.BatchItem.BatchId : r.BatchStage != null ? r.BatchStage.BatchItem.BatchId : null,
                r.StarterId))
            .ToListAsync(cancellationToken);
    }

    private sealed record DueRow(Guid Id, Guid OwnerId, DateTimeOffset DueAt);
}

/// <summary>The <c>push_subscriptions</c> table.</summary>
public sealed class PushSubscriptionRepository(OneLoafDbContext db) : IPushSubscriptionRepository
{
    /// <inheritdoc />
    public async Task<(PushSubscriptionDto Subscription, bool Created)> UpsertAsync(
        Guid userId, string endpoint, string p256dh, string auth, CancellationToken cancellationToken)
    {
        // xmax = 0 only for a freshly inserted row, so it tells an insert from an update of an existing endpoint.
        var row = await db.Database.SqlQuery<UpsertRow>($"""
            INSERT INTO push_subscriptions (user_id, endpoint, p256dh, auth)
            VALUES ({userId}, {endpoint}, {p256dh}, {auth})
            ON CONFLICT (endpoint) DO UPDATE SET user_id = EXCLUDED.user_id, p256dh = EXCLUDED.p256dh, auth = EXCLUDED.auth
            RETURNING id AS "Id", created_at AS "CreatedAt", (xmax = 0) AS "Created"
            """).ToListAsync(cancellationToken);
        var upserted = row[0];
        return (new PushSubscriptionDto(upserted.Id, endpoint, upserted.CreatedAt), upserted.Created);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        await db.PushSubscriptions.Where(s => s.Id == id && s.UserId == userId).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PushTarget>> ListTargetsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.PushSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => new PushTarget(s.Id, s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task DeleteExpiredAsync(Guid id, CancellationToken cancellationToken) =>
        db.PushSubscriptions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken);

    private sealed record UpsertRow(Guid Id, DateTimeOffset CreatedAt, bool Created);
}

/// <summary>Recipients from <c>users</c> and <c>kitchen_settings</c>.</summary>
public sealed class RecipientDirectory(OneLoafDbContext db) : IRecipientDirectory
{
    private const string DefaultTimeZone = "Europe/Vilnius";

    /// <inheritdoc />
    public Task<Recipient?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new Recipient(u.Id, u.Email, u.KitchenSettings != null ? u.KitchenSettings.TimeZone : DefaultTimeZone))
            .FirstOrDefaultAsync(cancellationToken);
}
