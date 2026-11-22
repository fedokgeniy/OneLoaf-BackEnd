using System.ComponentModel.DataAnnotations;

namespace OneLoaf.ServiceDefaults.Security;

/// <summary>JWT validation settings, from the environment (<c>Jwt__Issuer</c>, <c>Jwt__Audience</c>, <c>Jwt__PublicKeyPem</c>).</summary>
public sealed class JwtOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Expected <c>iss</c> of the token.</summary>
    [Required]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Expected <c>aud</c> of the token.</summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// RSA public key in PEM format. Line breaks may be written as the two characters <c>\n</c>, which is how a PEM
    /// fits into one line of <c>.env</c>.
    /// </summary>
    [Required]
    public string PublicKeyPem { get; set; } = string.Empty;

    /// <summary>Allowed clock difference between the issuer and this service.</summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; set; } = 30;
}
