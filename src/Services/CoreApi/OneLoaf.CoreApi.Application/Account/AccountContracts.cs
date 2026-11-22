using OneLoaf.Domain;
using OneLoaf.Domain.Entities;

namespace OneLoaf.CoreApi.Application.Account;

/// <summary>Profile of the current user (<c>GET /me</c>, login response).</summary>
/// <param name="Id">User id.</param>
/// <param name="Email">E-mail as entered at registration.</param>
/// <param name="DisplayName">Name shown in the public library.</param>
/// <param name="Role">Role: <c>user</c> or <c>admin</c>.</param>
/// <param name="CreatedAt">Registration time.</param>
/// <param name="LastLoginAt">Time of the previous successful login, if any.</param>
public sealed record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>Body of <c>DELETE /me</c> (US-04): the password confirms the deletion.</summary>
/// <param name="Password">Current password of the user.</param>
public sealed record DeleteAccountRequest(string Password);

/// <summary>What login and password confirmation need to know about a user.</summary>
/// <param name="Id">User id.</param>
/// <param name="Email">E-mail as entered.</param>
/// <param name="DisplayName">Display name.</param>
/// <param name="Role">Role.</param>
/// <param name="PasswordHash">Stored password hash.</param>
/// <param name="CreatedAt">Registration time.</param>
/// <param name="LastLoginAt">Previous login time.</param>
public sealed record UserCredentials(
    Guid Id,
    string Email,
    string DisplayName,
    UserRole Role,
    string PasswordHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt)
{
    /// <summary>The public part of the credentials.</summary>
    public UserProfileDto ToProfile() => new(Id, Email, DisplayName, Role, CreatedAt, LastLoginAt);
}

/// <summary>Data access for accounts (<c>users</c>, <c>kitchen_settings</c>).</summary>
public interface IUserRepository
{
    /// <summary>True when a user with this normalized e-mail exists.</summary>
    Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Saves a new user together with its kitchen settings in one transaction; returns the generated id.</summary>
    Task<Guid> AddAsync(User user, CancellationToken cancellationToken);

    /// <summary>Credentials of the user with this normalized e-mail, or null.</summary>
    Task<UserCredentials?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken);

    /// <summary>Credentials of the user with this id, or null.</summary>
    Task<UserCredentials?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Records a successful login and, when given, replaces the password hash (rehash).</summary>
    Task RecordLoginAsync(Guid userId, DateTimeOffset loggedInAt, string? newPasswordHash,
        CancellationToken cancellationToken);

    /// <summary>Profile of the user, or null.</summary>
    Task<UserProfileDto?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Deletes the user; the database cascades to the whole workspace. False when no row was deleted.</summary>
    Task<bool> DeleteAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Values of the <c>kitchen_settings</c> row created at registration (US-01: an empty workspace). Equipment and tariff
/// stay empty until the baker fills them in (US-13); packaging and loss start at zero.
/// </summary>
public static class KitchenSettingsDefaults
{
    /// <summary>Default currency (<c>currencies.code</c>).</summary>
    public const string Currency = "EUR";

    /// <summary>Default IANA time zone.</summary>
    public const string TimeZone = "Europe/Vilnius";

    /// <summary>Packaging cost per loaf.</summary>
    public const decimal PackagingCostPerLoaf = 0m;

    /// <summary>Loss rate, percent.</summary>
    public const decimal LossRatePercent = 0m;
}
