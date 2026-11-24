using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IRefreshTokenStore"/>.</summary>
public sealed class RefreshTokenStore(OneLoafDbContext db) : IRefreshTokenStore
{
    /// <inheritdoc />
    public async Task AddAsync(Guid userId, Guid familyId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            ExpiresAt = expiresAt.UtcDateTime,
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<StoredRefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var row = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
        return row is null
            ? null
            : new StoredRefreshToken(row.Id, row.UserId, row.FamilyId, Utc(row.ExpiresAt), Utc(row.RotatedAt), Utc(row.RevokedAt));
    }

    /// <inheritdoc />
    public async Task<bool> MarkRotatedAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(t => t.Id == id && t.RotatedAt == null && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RotatedAt, at.UtcDateTime), cancellationToken) == 1;

    /// <inheritdoc />
    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken) =>
        db.RefreshTokens.Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, at.UtcDateTime), cancellationToken);

    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static DateTimeOffset? Utc(DateTime? value) => value is { } v ? Utc(v) : null;
}
