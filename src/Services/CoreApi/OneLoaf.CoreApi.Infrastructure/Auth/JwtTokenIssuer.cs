using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.Domain;
using OneLoaf.ServiceDefaults.Security;

namespace OneLoaf.CoreApi.Infrastructure.Auth;

/// <summary>
/// Issues RS256 access tokens with <see cref="JsonWebTokenHandler"/> (no custom token format).
/// Singleton: owns the RSA private key.
/// </summary>
public sealed class JwtTokenIssuer : IAccessTokenIssuer, IDisposable
{
    private const string EscapedNewLine = "\\n";
    private readonly JwtIssuerOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly RSA _rsa;
    private readonly SigningCredentials _signingCredentials;
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    /// <summary>Loads the private key from <see cref="JwtIssuerOptions.SigningKeyPem"/>.</summary>
    public JwtTokenIssuer(IOptions<JwtIssuerOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider;

        _rsa = RSA.Create();
        try
        {
            _rsa.ImportFromPem(_options.SigningKeyPem.Replace(EscapedNewLine, "\n", StringComparison.Ordinal));
        }
        catch (ArgumentException ex)
        {
            _rsa.Dispose();
            throw new InvalidOperationException("Jwt:SigningKeyPem is not a valid PEM-encoded RSA private key.", ex);
        }

        // A factory per key: the default factory caches providers globally by key contents and would keep using this
        // RSA instance after it is disposed.
        var key = new RsaSecurityKey(_rsa) { CryptoProviderFactory = new CryptoProviderFactory() };
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
    }

    /// <inheritdoc />
    public AccessToken Issue(TokenSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [ClaimTypes.Subject] = subject.UserId.ToString(),
                [ClaimTypes.Email] = subject.Email,
                [ClaimTypes.Name] = subject.DisplayName,
                [ClaimTypes.Role] = ToClaimValue(subject.Role),
                [ClaimTypes.JwtId] = Guid.NewGuid().ToString("N"),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    /// <inheritdoc />
    public void Dispose() => _rsa.Dispose();

    private static string ToClaimValue(UserRole role) => role switch
    {
        UserRole.Admin => Roles.Admin,
        _ => Roles.User,
    };
}
