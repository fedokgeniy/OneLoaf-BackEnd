using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.CoreApi.Infrastructure.Auth;
using OneLoaf.CoreApi.UnitTests.Fakes;
using OneLoaf.Domain;
using OneLoaf.ServiceDefaults.Security;
using ClaimTypes = OneLoaf.ServiceDefaults.Security.ClaimTypes;

namespace OneLoaf.CoreApi.UnitTests.Auth;

public sealed class TokenAndHasherTests
{
    private const string Issuer = "https://localhost/core";
    private const string Audience = "oneloaf";

    [Fact]
    public async Task Issue_TokenPassesServiceDefaultsValidation_WithExpectedClaims()
    {
        using var rsa = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow;
        using var issuer = CreateIssuer(rsa.ExportRSAPrivateKeyPem(), now);
        var userId = Guid.NewGuid();

        var token = issuer.Issue(new TokenSubject(userId, "anna@example.com", "Anna", UserRole.Admin));

        Assert.Equal(now.AddMinutes(30), token.ExpiresAt);
        var result = await ValidateAsync(token.Token, rsa.ExportSubjectPublicKeyInfoPem());
        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal(userId.ToString(), result.Claims[ClaimTypes.Subject]);
        Assert.Equal("anna@example.com", result.Claims[ClaimTypes.Email]);
        Assert.Equal("Anna", result.Claims[ClaimTypes.Name]);
        Assert.Equal(Roles.Admin, result.Claims[ClaimTypes.Role]);
        Assert.True(result.Claims.ContainsKey(ClaimTypes.JwtId));
    }

    [Fact]
    public async Task Issue_TokenSignedWithAnotherKey_IsRejected()
    {
        using var signing = RSA.Create(2048);
        using var other = RSA.Create(2048);
        using var issuer = CreateIssuer(signing.ExportRSAPrivateKeyPem(), DateTimeOffset.UtcNow);

        var token = issuer.Issue(new TokenSubject(Guid.NewGuid(), "a@b.cd", "A", UserRole.User));

        Assert.False((await ValidateAsync(token.Token, other.ExportSubjectPublicKeyInfoPem())).IsValid);
    }

    [Fact]
    public async Task Issue_ExpiredToken_IsRejected()
    {
        using var rsa = RSA.Create(2048);
        using var issuer = CreateIssuer(rsa.ExportRSAPrivateKeyPem(), DateTimeOffset.UtcNow.AddHours(-2));

        var token = issuer.Issue(new TokenSubject(Guid.NewGuid(), "a@b.cd", "A", UserRole.User));

        Assert.False((await ValidateAsync(token.Token, rsa.ExportSubjectPublicKeyInfoPem())).IsValid);
    }

    [Fact]
    public void Constructor_PemWithEscapedNewLines_IsAccepted()
    {
        using var rsa = RSA.Create(2048);
        var escaped = rsa.ExportRSAPrivateKeyPem().Replace("\n", "\\n", StringComparison.Ordinal);

        using var issuer = CreateIssuer(escaped, DateTimeOffset.UtcNow);

        Assert.NotEmpty(issuer.Issue(new TokenSubject(Guid.NewGuid(), "a@b.cd", "A", UserRole.User)).Token);
    }

    [Fact]
    public void Constructor_InvalidPem_ThrowsClearError()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => CreateIssuer("not a key", DateTimeOffset.UtcNow));

        Assert.Contains("SigningKeyPem", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordHasher_ProducesIdentityV3HashThatVerifies()
    {
        var hasher = new PasswordHasherAdapter();

        var hash = hasher.Hash("secret-pass");

        // Identity v3 format: version byte 0x01, PRF, iteration count >= 100000 (fn_is_strong_password_hash).
        var bytes = Convert.FromBase64String(hash);
        Assert.Equal(0x01, bytes[0]);
        var iterations = (bytes[5] << 24) | (bytes[6] << 16) | (bytes[7] << 8) | bytes[8];
        Assert.True(iterations >= 100_000);
        Assert.Equal(PasswordCheck.Success, hasher.Verify(hash, "secret-pass"));
        Assert.Equal(PasswordCheck.Failed, hasher.Verify(hash, "wrong-pass"));
        Assert.Equal(PasswordCheck.Failed, hasher.Verify("not-base64!", "secret-pass"));
    }

    private static JwtTokenIssuer CreateIssuer(string privateKeyPem, DateTimeOffset now) =>
        new(Options.Create(new JwtIssuerOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKeyPem = privateKeyPem,
            AccessTokenMinutes = 30,
        }), new FixedTimeProvider(now));

    private static Task<Microsoft.IdentityModel.Tokens.TokenValidationResult> ValidateAsync(string token, string publicKeyPem)
    {
        var jwtOptions = Options.Create(new JwtOptions { Issuer = Issuer, Audience = Audience, PublicKeyPem = publicKeyPem });
        using var key = new JwtValidationKey(jwtOptions);
        var bearer = new JwtBearerOptions();
        new ConfigureJwtBearerOptions(jwtOptions, key).Configure(bearer);
        return new JsonWebTokenHandler().ValidateTokenAsync(token, bearer.TokenValidationParameters);
    }
}
