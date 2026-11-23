using OneLoaf.CoreApi.Application.Account;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.Application.Auth;

/// <summary>Body of <c>POST /auth/register</c> (US-01).</summary>
/// <param name="Email">Login e-mail.</param>
/// <param name="Password">Password, 8-128 characters.</param>
/// <param name="DisplayName">Name shown in the public library.</param>
public sealed record RegisterRequest(string Email, string Password, string DisplayName);

/// <summary>Result of a registration.</summary>
/// <param name="UserId">Id of the new user.</param>
public sealed record RegisterResponse(Guid UserId);

/// <summary>Body of <c>POST /auth/login</c> (US-02).</summary>
/// <param name="Email">Login e-mail.</param>
/// <param name="Password">Password.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>Result of a successful login.</summary>
/// <param name="AccessToken">Bearer token for the <c>Authorization</c> header.</param>
/// <param name="ExpiresAt">Expiry time of the token.</param>
/// <param name="User">Profile of the user.</param>
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserProfileDto User);

/// <summary>Password hashing (implemented with ASP.NET Core Identity <c>PasswordHasher</c>).</summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a password into the Identity v3 format accepted by <c>fn_is_strong_password_hash</c>.</summary>
    string Hash(string password);

    /// <summary>Checks a password against a stored hash.</summary>
    PasswordCheck Verify(string passwordHash, string password);
}

/// <summary>Result of <see cref="IPasswordHasher.Verify"/>.</summary>
public enum PasswordCheck
{
    /// <summary>The password does not match.</summary>
    Failed,

    /// <summary>The password matches.</summary>
    Success,

    /// <summary>The password matches, but the hash uses outdated parameters and should be replaced.</summary>
    SuccessRehashNeeded,
}

/// <summary>Issues access tokens (RS256 JWT).</summary>
public interface IAccessTokenIssuer
{
    /// <summary>Creates a token for the user.</summary>
    AccessToken Issue(TokenSubject subject);
}

/// <summary>Claims of an access token.</summary>
/// <param name="UserId">Claim <c>sub</c>.</param>
/// <param name="Email">Claim <c>email</c>.</param>
/// <param name="DisplayName">Claim <c>name</c>.</param>
/// <param name="Role">Claim <c>role</c>.</param>
public sealed record TokenSubject(Guid UserId, string Email, string DisplayName, UserRole Role);

/// <summary>An issued token.</summary>
/// <param name="Token">Compact JWT.</param>
/// <param name="ExpiresAt">Expiry time.</param>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>E-mail normalization; must match <c>ck_users_normalized_email</c> (<c>normalized_email = upper(email)</c>).</summary>
public static class EmailNormalizer
{
    /// <summary>The e-mail as stored: surrounding spaces removed.</summary>
    public static string Clean(string email)
    {
        ArgumentNullException.ThrowIfNull(email);
        return email.Trim();
    }

    /// <summary>The lookup key: cleaned and upper-cased.</summary>
    public static string Normalize(string email) => Clean(email).ToUpperInvariant();
}
