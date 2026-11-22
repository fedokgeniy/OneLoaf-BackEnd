using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace OneLoaf.ServiceDefaults.Security;

/// <summary>Configures JWT bearer validation from <see cref="JwtOptions"/>: RS256 only, issuer, audience, lifetime.</summary>
public sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions, JwtValidationKey key)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    /// <inheritdoc />
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            Configure(options);
        }
    }

    /// <inheritdoc />
    public void Configure(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var jwt = jwtOptions.Value;

        // Keep the short claim names (sub, role, email) instead of the legacy WS-* URIs.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key.SecurityKey,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
            NameClaimType = ClaimTypes.Subject,
            RoleClaimType = ClaimTypes.Role,
        };
    }
}
