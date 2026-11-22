namespace OneLoaf.SharedKernel.Security;

/// <summary>
/// The user of the current request, taken from the validated JWT (claim <c>sub</c>), never from the request body
/// (CLAUDE.md rule 3). Every query of workspace data filters by <see cref="Id"/>.
/// </summary>
public interface ICurrentUser
{
    /// <summary>True when the request carries a valid token.</summary>
    bool IsAuthenticated { get; }

    /// <summary>Id of the authenticated user. Throws when the request is anonymous.</summary>
    Guid Id { get; }

    /// <summary>Role of the user (<c>user</c> or <c>admin</c>), or null when anonymous.</summary>
    string? Role { get; }

    /// <summary>True when the user has the administrator role.</summary>
    bool IsAdmin { get; }
}
