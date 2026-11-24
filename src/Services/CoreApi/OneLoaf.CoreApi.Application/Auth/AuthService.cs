using Microsoft.Extensions.Logging;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.Domain;
using OneLoaf.Domain.Entities;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.Application.Auth;

/// <summary>Registration (US-01) and login (US-02).</summary>
public sealed partial class AuthService(
    IUserRepository users,
    IPasswordHasher passwordHasher,
    IAccessTokenIssuer tokenIssuer,
    IRefreshTokenStore refreshTokens,
    SessionOptions session,
    TimeProvider timeProvider,
    ILogger<AuthService> logger)
{
    /// <summary>Message for a duplicate e-mail (409).</summary>
    public const string EmailTakenMessage = "An account with this e-mail already exists.";

    private const string DummyPassword = "timing-equalization-only";
    private static string? _dummyHash;

    /// <summary>
    /// Creates the account and its empty workspace (<c>users</c> + <c>kitchen_settings</c> in one transaction).
    /// A concurrent duplicate is still rejected by the unique index (23505 → 409).
    /// </summary>
    /// <exception cref="ConflictException">The e-mail is already registered.</exception>
    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = EmailNormalizer.Clean(request.Email);
        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        if (await users.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException(EmailTakenMessage);
        }

        var user = new User
        {
            Email = email,
            NormalizedEmail = normalizedEmail,
            PasswordHash = passwordHasher.Hash(request.Password),
            DisplayName = request.DisplayName.Trim(),
            Role = UserRole.User,
            KitchenSettings = new KitchenSettings
            {
                PackagingCostPerLoaf = KitchenSettingsDefaults.PackagingCostPerLoaf,
                LossRatePercent = KitchenSettingsDefaults.LossRatePercent,
                Currency = KitchenSettingsDefaults.Currency,
                TimeZone = KitchenSettingsDefaults.TimeZone,
            },
        };

        var userId = await users.AddAsync(user, cancellationToken);
        LogRegistered(logger, userId);
        return new RegisterResponse(userId);
    }

    /// <summary>
    /// Checks the credentials and issues an access token and the first refresh token of a new family. Returns null for
    /// an unknown e-mail and for a wrong password alike, and spends the same hashing time in both cases, so the
    /// response does not reveal which accounts exist.
    /// </summary>
    public async Task<SessionTokens?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var credentials = await users.FindByEmailAsync(EmailNormalizer.Normalize(request.Email), cancellationToken);
        if (credentials is null)
        {
            var dummyHash = LazyInitializer.EnsureInitialized(ref _dummyHash, () => passwordHasher.Hash(DummyPassword));
            passwordHasher.Verify(dummyHash, request.Password);
            LogLoginFailed(logger);
            return null;
        }

        var check = passwordHasher.Verify(credentials.PasswordHash, request.Password);
        if (check == PasswordCheck.Failed)
        {
            LogLoginFailed(logger);
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var newHash = check == PasswordCheck.SuccessRehashNeeded ? passwordHasher.Hash(request.Password) : null;
        await users.RecordLoginAsync(credentials.Id, now, newHash, cancellationToken);

        LogLoggedIn(logger, credentials.Id);
        return await IssueAsync(credentials, Guid.NewGuid(), now, credentials.ToProfile() with { LastLoginAt = now }, cancellationToken);
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token and the next refresh token of the same family (rotation).
    /// Returns null for an unknown, expired or revoked token. A token that was already exchanged is a sign that it was
    /// copied: its whole family is revoked, so neither the thief nor the owner can continue without the password.
    /// </summary>
    public async Task<SessionTokens?> RefreshAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var stored = await refreshTokens.FindAsync(RefreshTokenValues.Hash(refreshToken), cancellationToken);
        if (stored is null || stored.RevokedAt is not null || stored.ExpiresAt <= now)
        {
            return null;
        }

        if (stored.RotatedAt is { } rotatedAt && now - rotatedAt > session.RotationGrace)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, now, cancellationToken);
            LogReuse(logger, stored.UserId);
            return null;
        }

        if (stored.RotatedAt is null)
        {
            // False when a parallel request exchanged the token a moment ago: the same case as the grace period above
            // (two tabs of one browser, a repeated request), so the sign-in continues.
            await refreshTokens.MarkRotatedAsync(stored.Id, now, cancellationToken);
        }

        var credentials = await users.FindByIdAsync(stored.UserId, cancellationToken);
        return credentials is null
            ? null
            : await IssueAsync(credentials, stored.FamilyId, now, credentials.ToProfile(), cancellationToken);
    }

    /// <summary>Signs out: revokes the family of the refresh token, so the browser cannot get new access tokens.</summary>
    public async Task LogoutAsync(string? refreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        if (await refreshTokens.FindAsync(RefreshTokenValues.Hash(refreshToken), cancellationToken) is { } stored)
        {
            await refreshTokens.RevokeFamilyAsync(stored.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
        }
    }

    private async Task<SessionTokens> IssueAsync(
        UserCredentials credentials, Guid familyId, DateTimeOffset now, UserProfileDto profile, CancellationToken cancellationToken)
    {
        var access = tokenIssuer.Issue(new TokenSubject(credentials.Id, credentials.Email, credentials.DisplayName, credentials.Role));
        var refresh = new IssuedRefreshToken(RefreshTokenValues.Create(), now.Add(session.RefreshTokenLifetime));
        await refreshTokens.AddAsync(credentials.Id, familyId, RefreshTokenValues.Hash(refresh.Value), refresh.ExpiresAt, cancellationToken);
        return new SessionTokens(new LoginResponse(access.Token, access.ExpiresAt, profile), refresh);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} registered")]
    private static partial void LogRegistered(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} logged in")]
    private static partial void LogLoggedIn(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse for user {UserId}: the session was revoked")]
    private static partial void LogReuse(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login failed: invalid credentials")]
    private static partial void LogLoginFailed(ILogger logger);
}
