using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Layout;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Account;

/// <summary>Side effects of the <see cref="SessionState"/> slice: sign-in, registration, sign-out, expiry.</summary>
public sealed class SessionEffects(AccountApi account, TokenStore tokens, SessionRefresher refresher, NavigationManager navigation)
{
    /// <summary>Signs in; stores the token, then reports <see cref="LoggedIn"/> or <see cref="LoginFailed"/>.</summary>
    [EffectMethod]
    public async Task HandleLogin(Login action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = await account.LoginAsync(new LoginRequest(action.Email.Trim(), action.Password));
        switch (result)
        {
            case ApiResult<LoginResponse>.Success { Value: var login }:
                await tokens.SetAsync(login.AccessToken, login.ExpiresAt);
                dispatcher.Dispatch(new LoggedIn(SessionUser.From(login.User), login.ExpiresAt));
                break;
            case ApiResult<LoginResponse>.Failure { Error: var error }:
                dispatcher.Dispatch(new LoginFailed(error));
                break;
        }
    }

    /// <summary>Creates the account and signs in with it (US-01: signed in right after registration).</summary>
    [EffectMethod]
    public async Task HandleRegister(Register action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var request = new RegisterRequest(action.Email.Trim(), action.Password, action.DisplayName.Trim());
        var result = await account.RegisterAsync(request);
        switch (result)
        {
            case ApiResult<RegisterResponse>.Success:
                dispatcher.Dispatch(new Login(request.Email, request.Password));
                break;
            case ApiResult<RegisterResponse>.Failure { Error: var error }:
                dispatcher.Dispatch(new RegisterFailed(error));
                break;
        }
    }

    /// <summary>Signs out: tells the API, drops the token, goes to the sign-in page.</summary>
    [EffectMethod(typeof(Logout))]
    public async Task HandleLogout(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        // The API revokes the refresh token; a failed call does not keep the user signed in here.
        await account.LogoutAsync();
        await tokens.ClearAsync();
        dispatcher.Dispatch(new LoggedOut());
        navigation.NavigateTo(ReturnUrl.LoginPath);
    }

    /// <summary>Session ended (expiry or 401): drop the token, explain, return here after signing in again.</summary>
    [EffectMethod(typeof(SessionExpired))]
    public async Task HandleSessionExpired(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        await tokens.ClearAsync();
        dispatcher.Dispatch(new ShowToast(Toast.Warning("Your session has ended. Please sign in again.")));
        var current = "/" + navigation.ToBaseRelativePath(navigation.Uri);
        navigation.NavigateTo(ReturnUrl.LoginUrl(current));
    }

    /// <summary>
    /// Gets a new access token with the refresh cookie, without bothering the user. When the sign-in has ended (cookie
    /// expired, revoked or missing) the session ends as before.
    /// </summary>
    [EffectMethod(typeof(RefreshSession))]
    public async Task HandleRefresh(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (await refresher.TryRefreshAsync() && tokens.ValidToken is { } token && tokens.ExpiresAt is { } expiresAt
            && SessionUser.From(JwtClaims.ToPrincipal(token)) is { } user)
        {
            dispatcher.Dispatch(new SessionRestored(user, expiresAt));
            return;
        }

        dispatcher.Dispatch(new SessionExpired());
    }
}
