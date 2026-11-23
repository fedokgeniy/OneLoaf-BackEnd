using System.Buffers.Text;
using System.Security.Claims;
using System.Text.Json;

namespace OneLoaf.Web.Shared.Auth;

/// <summary>
/// Reads the claims of a JWT payload for display and routing only. The signature is not checked here: the services
/// validate every request, the client never trusts the claims for authorization of data.
/// </summary>
public static class JwtClaims
{
    /// <summary>Authentication type of the identity built from the token.</summary>
    public const string AuthenticationType = "oneloaf-jwt";

    /// <summary>Claim names used by the Core API token (docs/api/guides/authentication.md).</summary>
    public const string Subject = "sub";

    /// <summary>E-mail claim.</summary>
    public const string Email = "email";

    /// <summary>Display name claim.</summary>
    public const string Name = "name";

    /// <summary>Role claim (<c>user</c> or <c>admin</c>).</summary>
    public const string Role = "role";

    private static readonly Dictionary<string, string> LongNames = new(StringComparer.Ordinal)
    {
        [ClaimTypes.NameIdentifier] = Subject,
        [ClaimTypes.Email] = Email,
        [ClaimTypes.Name] = Name,
        [ClaimTypes.Role] = Role,
    };

    /// <summary>Builds a principal from the token; an unreadable token gives an anonymous principal.</summary>
    public static ClaimsPrincipal ToPrincipal(string token)
    {
        var claims = Parse(token);
        return claims.Count == 0
            ? new ClaimsPrincipal(new ClaimsIdentity())
            : new ClaimsPrincipal(new ClaimsIdentity(claims, AuthenticationType, Name, Role));
    }

    /// <summary>Returns the claims of the payload, or an empty list when the token is not a readable JWT.</summary>
    public static IReadOnlyList<Claim> Parse(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return [];
        }

        try
        {
            var payload = Base64Url.DecodeFromChars(parts[1]);
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            var claims = new List<Claim>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var type = LongNames.GetValueOrDefault(property.Name, property.Name);
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    claims.AddRange(property.Value.EnumerateArray().Select(v => new Claim(type, ValueOf(v))));
                }
                else
                {
                    claims.Add(new Claim(type, ValueOf(property.Value)));
                }
            }

            return claims;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return [];
        }
    }

    private static string ValueOf(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.GetRawText();
}
