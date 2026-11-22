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
    TimeProvider timeProvider,
    ILogger<AuthService> logger)
{
    /// <summary>Message for a duplicate e-mail (409).</summary>
    public const string EmailTakenMessage = "An account with this e-mail already exists.";

    private const string DummyPassword = "timing-equalization-only";
    private static string? s_dummyHash;

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
    /// Checks the credentials and issues a token. Returns null for an unknown e-mail and for a wrong password alike,
    /// and spends the same hashing time in both cases, so the response does not reveal which accounts exist.
    /// </summary>
    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var credentials = await users.FindByEmailAsync(EmailNormalizer.Normalize(request.Email), cancellationToken);
        if (credentials is null)
        {
            var dummyHash = LazyInitializer.EnsureInitialized(ref s_dummyHash, () => passwordHasher.Hash(DummyPassword));
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

        var token = tokenIssuer.Issue(new TokenSubject(credentials.Id, credentials.Email, credentials.DisplayName,
            credentials.Role));
        LogLoggedIn(logger, credentials.Id);
        return new LoginResponse(token.Token, token.ExpiresAt, credentials.ToProfile() with { LastLoginAt = now });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} registered")]
    private static partial void LogRegistered(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} logged in")]
    private static partial void LogLoggedIn(ILogger logger, Guid userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Login failed: invalid credentials")]
    private static partial void LogLoginFailed(ILogger logger);
}
