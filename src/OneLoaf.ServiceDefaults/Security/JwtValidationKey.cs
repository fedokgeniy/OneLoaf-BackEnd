using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace OneLoaf.ServiceDefaults.Security;

/// <summary>
/// The RSA public key used to validate access tokens (RS256). Singleton: owns and disposes the
/// <see cref="RSA"/> instance.
/// </summary>
public sealed class JwtValidationKey : IDisposable
{
    private const string EscapedNewLine = "\\n";
    private readonly RSA _rsa;

    /// <summary>Loads the key from <see cref="JwtOptions.PublicKeyPem"/>.</summary>
    public JwtValidationKey(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var pem = options.Value.PublicKeyPem.Replace(EscapedNewLine, "\n", StringComparison.Ordinal);
        _rsa = RSA.Create();
        try
        {
            _rsa.ImportFromPem(pem);
        }
        catch (ArgumentException ex)
        {
            _rsa.Dispose();
            throw new InvalidOperationException("Jwt:PublicKeyPem is not a valid PEM-encoded RSA public key.", ex);
        }

        // A factory per key: the default factory caches providers globally by key contents and would keep using this
        // RSA instance after it is disposed.
        SecurityKey = new RsaSecurityKey(_rsa) { CryptoProviderFactory = new CryptoProviderFactory() };
    }

    /// <summary>Key for <see cref="TokenValidationParameters.IssuerSigningKey"/>.</summary>
    public SecurityKey SecurityKey { get; }

    /// <inheritdoc />
    public void Dispose() => _rsa.Dispose();
}
