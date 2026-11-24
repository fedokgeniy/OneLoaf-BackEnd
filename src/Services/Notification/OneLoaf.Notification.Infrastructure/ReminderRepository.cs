using Microsoft.EntityFrameworkCore;
using OneLoaf.Domain;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.Notification.Application.Reminders;
using OneLoaf.SharedKernel.Text;

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
        if (WireEnumNames.ParseOrNull<ReminderStatus>(query.Status) is { } status)
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
