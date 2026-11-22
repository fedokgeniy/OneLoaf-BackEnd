using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.ServiceDefaults.Security;

/// <summary><see cref="ICurrentUser"/> read from the claims of the validated token of the current request.</summary>
public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    /// <inheritdoc />
    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true && TryGetId(out _);

    /// <inheritdoc />
    public Guid Id => TryGetId(out var id)
        ? id
        : throw new InvalidOperationException(
            "No authenticated user in the current request. Endpoints that use ICurrentUser must require authorization.");

    /// <inheritdoc />
    public string? Role => IsAuthenticated ? Principal!.FindFirstValue(ClaimTypes.Role) : null;

    /// <inheritdoc />
    public bool IsAdmin => string.Equals(Role, Roles.Admin, StringComparison.Ordinal);

    private bool TryGetId(out Guid id)
    {
        id = Guid.Empty;
        var subject = Principal?.Identity?.IsAuthenticated == true ? Principal.FindFirstValue(ClaimTypes.Subject) : null;
        return Guid.TryParse(subject, out id) && id != Guid.Empty;
    }
}
