using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IUserRepository"/>. Reads are no-tracking projections.</summary>
public sealed class UserRepository(OneLoafDbContext db) : IUserRepository
{
    /// <inheritdoc />
    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

    /// <inheritdoc />
    public async Task<Guid> AddAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        // One SaveChanges = one transaction for users + kitchen_settings; id and created_at come from the database.
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        return user.Id;
    }

    /// <inheritdoc />
    public Task<UserCredentials?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        SelectCredentials(db.Users.Where(u => u.NormalizedEmail == normalizedEmail))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<UserCredentials?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        SelectCredentials(db.Users.Where(u => u.Id == userId)).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task RecordLoginAsync(Guid userId, DateTimeOffset loggedInAt, string? newPasswordHash,
        CancellationToken cancellationToken)
    {
        var at = loggedInAt.UtcDateTime;
        var query = db.Users.Where(u => u.Id == userId);
        return newPasswordHash is null
            ? query.ExecuteUpdateAsync(s => s.SetProperty(u => u.LastLoginAt, at), cancellationToken)
            : query.ExecuteUpdateAsync(s => s
                .SetProperty(u => u.LastLoginAt, at)
                .SetProperty(u => u.PasswordHash, newPasswordHash), cancellationToken);
    }

    /// <inheritdoc />
    public Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfileDto(
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role,
                new DateTimeOffset(u.CreatedAt, TimeSpan.Zero),
                u.LastLoginAt == null ? null : new DateTimeOffset(u.LastLoginAt.Value, TimeSpan.Zero)))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken) > 0;

    private static IQueryable<UserCredentials> SelectCredentials(IQueryable<User> users) =>
        users.AsNoTracking().Select(u => new UserCredentials(
            u.Id,
            u.Email,
            u.DisplayName,
            u.Role,
            u.PasswordHash,
            new DateTimeOffset(u.CreatedAt, TimeSpan.Zero),
            u.LastLoginAt == null ? null : new DateTimeOffset(u.LastLoginAt.Value, TimeSpan.Zero)));
}
