using Microsoft.EntityFrameworkCore;
using OneLoaf.Infrastructure.Persistence;
using OneLoaf.Notification.Application.Push;

namespace OneLoaf.Notification.Infrastructure;

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
