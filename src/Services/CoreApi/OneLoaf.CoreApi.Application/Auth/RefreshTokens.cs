using System.Security.Cryptography;

namespace OneLoaf.CoreApi.Application.Auth;

/// <summary>How long a sign-in lasts without the password being entered again.</summary>
/// <param name="RefreshTokenLifetime">Lifetime of a refresh token; each use issues a new token with a new lifetime.</param>
public sealed record SessionOptions(TimeSpan RefreshTokenLifetime);

/// <summary>A refresh token as stored: only its hash is kept.</summary>
/// <param name="Id">Row id.</param>
/// <param name="UserId">Owner.</param>
/// <param name="FamilyId">Chain of tokens that started with one sign-in.</param>
/// <param name="ExpiresAt">End of validity.</param>
/// <param name="RotatedAt">When the token was exchanged for the next one; null while it is the current token.</param>
/// <param name="RevokedAt">When the chain was revoked (sign-out or reuse of an exchanged token).</param>
public sealed record StoredRefreshToken(
    Guid Id, Guid UserId, Guid FamilyId, DateTimeOffset ExpiresAt, DateTimeOffset? RotatedAt, DateTimeOffset? RevokedAt);

/// <summary>Data access for <c>refresh_tokens</c>.</summary>
public interface IRefreshTokenStore
{
    /// <summary>Stores the hash of a new token.</summary>
    Task AddAsync(Guid userId, Guid familyId, string tokenHash, DateTimeOffset expiresAt, CancellationToken cancellationToken);

    /// <summary>The token with this hash, or null.</summary>
    Task<StoredRefreshToken?> FindAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>
    /// Marks the token as exchanged; false when another request exchanged it first, so the same token is never
    /// exchanged twice.
    /// </summary>
    Task<bool> MarkRotatedAsync(Guid id, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Revokes every token of the chain.</summary>
    Task RevokeFamilyAsync(Guid familyId, DateTimeOffset at, CancellationToken cancellationToken);
}

/// <summary>A new refresh token: the value goes to the browser cookie, the hash to the database.</summary>
/// <param name="Value">Random value, URL-safe.</param>
/// <param name="ExpiresAt">End of validity.</param>
public sealed record IssuedRefreshToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>Result of a sign-in or of a token exchange: the response body and the refresh token for the cookie.</summary>
/// <param name="Response">Access token and profile.</param>
/// <param name="RefreshToken">Refresh token to set as an HttpOnly cookie.</param>
public sealed record SessionTokens(LoginResponse Response, IssuedRefreshToken RefreshToken);

/// <summary>Creates and hashes refresh token values.</summary>
public static class RefreshTokenValues
{
    private const int Bytes = 32;

    /// <summary>A new random value: 256 bits from the system generator, base64url.</summary>
    public static string Create() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(Bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// SHA-256 of the value, hex. The value is long and random, so a plain hash is enough: a leaked table gives no
    /// usable tokens.
    /// </summary>
    public static string Hash(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value)));
    }
}
