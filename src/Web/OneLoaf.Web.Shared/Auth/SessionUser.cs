using System.Security.Claims;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Shared.Auth;

/// <summary>The signed-in user as the UI needs it (header, avatar menu, admin-only routes).</summary>
public sealed record SessionUser(Guid Id, string Email, string DisplayName, string Role)
{
    /// <summary>Role name of moderators.</summary>
    public const string AdminRole = "admin";

    /// <summary>The user may open the moderation screens.</summary>
    public bool IsAdmin => string.Equals(Role, AdminRole, StringComparison.OrdinalIgnoreCase);

    /// <summary>From the login response.</summary>
    public static SessionUser From(UserDto user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new SessionUser(user.Id, user.Email, user.DisplayName, user.Role);
    }

    /// <summary>From the claims of a restored token; <see langword="null"/> when the token has no subject.</summary>
    public static SessionUser? From(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);
        if (!Guid.TryParse(principal.FindFirst(JwtClaims.Subject)?.Value, out var id))
        {
            return null;
        }

        var email = principal.FindFirst(JwtClaims.Email)?.Value ?? string.Empty;
        var name = principal.FindFirst(JwtClaims.Name)?.Value;
        return new SessionUser(id, email, string.IsNullOrWhiteSpace(name) ? email : name,
            principal.FindFirst(JwtClaims.Role)?.Value ?? "user");
    }
}
