using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.Domain.Entities;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.UnitTests.Fakes;

/// <summary>In-memory <see cref="IUserRepository"/>.</summary>
internal sealed class FakeUserRepository : IUserRepository
{
    public static readonly DateTimeOffset CreatedAt = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);

    public List<User> Users { get; } = [];

    public List<(Guid UserId, DateTimeOffset At, string? NewHash)> Logins { get; } = [];

    public Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Users.Exists(u => u.NormalizedEmail == normalizedEmail));

    public Task<Guid> AddAsync(User user, CancellationToken cancellationToken)
    {
        user.Id = Guid.NewGuid();
        user.CreatedAt = CreatedAt.UtcDateTime;
        Users.Add(user);
        return Task.FromResult(user.Id);
    }

    public Task<UserCredentials?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) =>
        Task.FromResult(ToCredentials(Users.Find(u => u.NormalizedEmail == normalizedEmail)));

    public Task<UserCredentials?> FindByIdAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(ToCredentials(Users.Find(u => u.Id == userId)));

    public Task RecordLoginAsync(Guid userId, DateTimeOffset loggedInAt, string? newPasswordHash,
        CancellationToken cancellationToken)
    {
        Logins.Add((userId, loggedInAt, newPasswordHash));
        var user = Users.Single(u => u.Id == userId);
        user.LastLoginAt = loggedInAt.UtcDateTime;
        if (newPasswordHash is not null)
        {
            user.PasswordHash = newPasswordHash;
        }

        return Task.CompletedTask;
    }

    public Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(ToCredentials(Users.Find(u => u.Id == userId))?.ToProfile());

    public Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Users.RemoveAll(u => u.Id == userId) > 0);

    public User Seed(string email, string passwordHash)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = passwordHash,
            DisplayName = "Anna",
            CreatedAt = CreatedAt.UtcDateTime,
        };
        Users.Add(user);
        return user;
    }

    private static UserCredentials? ToCredentials(User? u) => u is null
        ? null
        : new UserCredentials(u.Id, u.Email, u.DisplayName, u.Role, u.PasswordHash,
            new DateTimeOffset(u.CreatedAt, TimeSpan.Zero),
            u.LastLoginAt is null ? null : new DateTimeOffset(u.LastLoginAt.Value, TimeSpan.Zero));
}

/// <summary>Readable fake hash: <c>hash:{password}</c>; <c>old:{password}</c> verifies with "rehash needed".</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public const string Prefix = "hash:";
    public const string OldPrefix = "old:";

    public int VerifyCalls { get; private set; }

    public string Hash(string password) => Prefix + password;

    public PasswordCheck Verify(string passwordHash, string password)
    {
        VerifyCalls++;
        if (passwordHash == Prefix + password)
        {
            return PasswordCheck.Success;
        }

        return passwordHash == OldPrefix + password ? PasswordCheck.SuccessRehashNeeded : PasswordCheck.Failed;
    }
}

internal sealed class FakeTokenIssuer : IAccessTokenIssuer
{
    public TokenSubject? LastSubject { get; private set; }

    public AccessToken Issue(TokenSubject subject)
    {
        LastSubject = subject;
        return new AccessToken("token-for-" + subject.UserId, new DateTimeOffset(2026, 11, 23, 13, 0, 0, TimeSpan.Zero));
    }
}

internal sealed class FakeCurrentUser(Guid id) : ICurrentUser
{
    public bool IsAuthenticated => true;

    public Guid Id { get; } = id;

    public string? Role => "user";

    public bool IsAdmin => false;
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeRefreshTokenStore : OneLoaf.CoreApi.Application.Auth.IRefreshTokenStore
{
    public List<Row> Tokens { get; } = [];

    public Task AddAsync(Guid userId, Guid familyId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        Tokens.Add(new Row { Id = Guid.NewGuid(), UserId = userId, FamilyId = familyId, Hash = tokenHash, ExpiresAt = expiresAt });
        return Task.CompletedTask;
    }

    public Task<OneLoaf.CoreApi.Application.Auth.StoredRefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken) =>
        Task.FromResult(Tokens.Find(t => t.Hash == tokenHash) is { } t
            ? new OneLoaf.CoreApi.Application.Auth.StoredRefreshToken(t.Id, t.UserId, t.FamilyId, t.ExpiresAt, t.RotatedAt, t.RevokedAt)
            : null);

    public Task<bool> MarkRotatedAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken)
    {
        var token = Tokens.Find(t => t.Id == id);
        if (token is null || token.RotatedAt is not null || token.RevokedAt is not null)
        {
            return Task.FromResult(false);
        }

        token.RotatedAt = at;
        return Task.FromResult(true);
    }

    public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        foreach (var token in Tokens.Where(t => t.FamilyId == familyId))
        {
            token.RevokedAt ??= at;
        }

        return Task.CompletedTask;
    }

    internal sealed class Row
    {
        public Guid Id { get; init; }

        public Guid UserId { get; init; }

        public Guid FamilyId { get; init; }

        public string Hash { get; init; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; init; }

        public DateTimeOffset? RotatedAt { get; set; }

        public DateTimeOffset? RevokedAt { get; set; }
    }
}
