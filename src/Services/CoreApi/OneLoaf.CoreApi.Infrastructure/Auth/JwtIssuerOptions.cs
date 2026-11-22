using System.ComponentModel.DataAnnotations;

namespace OneLoaf.CoreApi.Infrastructure.Auth;

/// <summary>
/// Token issuing settings of the Core API (<c>Jwt__SigningKeyPem</c>, <c>Jwt__AccessTokenMinutes</c>, plus the shared
/// <c>Jwt__Issuer</c> and <c>Jwt__Audience</c>). The private key exists only in the Core API environment.
/// </summary>
public sealed class JwtIssuerOptions
{
    /// <summary>Configuration section name (shared with the validation settings).</summary>
    public const string SectionName = "Jwt";

    /// <summary>Value of <c>iss</c>.</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Value of <c>aud</c>.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>RSA private key in PEM format; line breaks may be written as <c>\n</c>.</summary>
    [Required]
    public string SigningKeyPem { get; set; } = string.Empty;

    /// <summary>Lifetime of an access token (architecture.md §4: 60 minutes).</summary>
    [Range(5, 1440)]
    public int AccessTokenMinutes { get; set; } = 60;
}
