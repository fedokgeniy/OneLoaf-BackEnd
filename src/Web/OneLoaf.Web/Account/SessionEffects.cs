using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Layout;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Account;

/// <summary>Side effects of the <see cref="SessionState"/> slice: sign-in, registration, sign-out, expiry.</summary>
public sealed class SessionEffects(AccountApi account, TokenStore tokens, NavigationManager navigation)
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
        // The token is stateless; a failed logout call does not keep the user signed in.
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

    /// <summary>One minute before expiry: warn, so the user can finish and sign in again.</summary>
    [EffectMethod(typeof(SessionEndingSoon))]
    public static Task HandleSessionEndingSoon(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.Dispatch(new ShowToast(Toast.Warning(
            "Your session ends in about a minute. Save your work; you will need to sign in again.")));
        return Task.CompletedTask;
    }
}
