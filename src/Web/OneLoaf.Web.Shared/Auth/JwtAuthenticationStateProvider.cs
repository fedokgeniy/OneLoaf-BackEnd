using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace OneLoaf.Web.Shared.Auth;

/// <summary>
/// Authentication state for <c>AuthorizeRouteView</c> and <c>[Authorize]</c>: the claims of the valid token in the
/// <see cref="TokenStore"/>, anonymous otherwise. Re-evaluated whenever the token changes.
/// </summary>
public sealed class JwtAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));
    private readonly TokenStore _tokens;

    /// <summary>Creates the provider and follows changes of the token.</summary>
    public JwtAuthenticationStateProvider(TokenStore tokens)
    {
        _tokens = tokens;
        _tokens.Changed += OnTokenChanged;
    }

    /// <inheritdoc />
    public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
        Task.FromResult(_tokens.ValidToken is { } token ? new AuthenticationState(JwtClaims.ToPrincipal(token)) : Anonymous);

    /// <inheritdoc />
    public void Dispose() => _tokens.Changed -= OnTokenChanged;

    private void OnTokenChanged(object? sender, EventArgs e) => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
