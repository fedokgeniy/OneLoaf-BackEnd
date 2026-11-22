using Microsoft.AspNetCore.Identity;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.Domain.Entities;

namespace OneLoaf.CoreApi.Infrastructure.Auth;

/// <summary>
/// <see cref="IPasswordHasher"/> over ASP.NET Core Identity <see cref="PasswordHasher{TUser}"/> (Identity v3 format:
/// PBKDF2-HMAC-SHA512, 100 000 iterations, 128-bit salt), the format required by <c>fn_is_strong_password_hash</c>.
/// No custom cryptography (requirement C7).
/// </summary>
public sealed class PasswordHasherAdapter : IPasswordHasher
{
    // PasswordHasher does not use the user instance for the v3 format.
    private static readonly User HashUser = new();
    private readonly PasswordHasher<User> _hasher = new();

    /// <inheritdoc />
    public string Hash(string password) => _hasher.HashPassword(HashUser, password);

    /// <inheritdoc />
    /// <remarks>A malformed stored hash (not Base64) counts as a failed check instead of a 500.</remarks>
    public PasswordCheck Verify(string passwordHash, string password)
    {
        try
        {
            return _hasher.VerifyHashedPassword(HashUser, passwordHash, password) switch
            {
                PasswordVerificationResult.Success => PasswordCheck.Success,
                PasswordVerificationResult.SuccessRehashNeeded => PasswordCheck.SuccessRehashNeeded,
                _ => PasswordCheck.Failed,
            };
        }
        catch (FormatException)
        {
            return PasswordCheck.Failed;
        }
    }
}
