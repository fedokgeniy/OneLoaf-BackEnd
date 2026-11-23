namespace OneLoaf.ServiceDefaults.Security;

/// <summary>
/// Claim names of the OneLoaf access token. Short JWT names are kept as issued:
/// inbound claim mapping is switched off.
/// </summary>
public static class ClaimTypes
{
    /// <summary>User id (UUID).</summary>
    public const string Subject = "sub";

    /// <summary>E-mail of the user.</summary>
    public const string Email = "email";

    /// <summary>Role, one of <see cref="Roles"/>.</summary>
    public const string Role = "role";

    /// <summary>Display name.</summary>
    public const string Name = "name";

    /// <summary>Unique token id.</summary>
    public const string JwtId = "jti";
}
