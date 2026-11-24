using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Account;

/// <summary>
/// Gets a new access token with the refresh token cookie (US-02). The cookie is HttpOnly and never visible to this
/// code: the browser sends it to the Core API, which answers with the next access token. Several callers at once
/// share one request, because every exchange replaces the cookie.
/// </summary>
public sealed class SessionRefresher(AccountApi account, TokenStore tokens)
{
    private Task<bool>? _running;

    /// <summary>True when a new access token was stored; false when the sign-in has ended or the API is unreachable.</summary>
    public Task<bool> TryRefreshAsync() => _running ??= RunAsync();

    private async Task<bool> RunAsync()
    {
        try
        {
            if (await account.RefreshAsync() is ApiResult<LoginResponse>.Success { Value: var session })
            {
                await tokens.SetAsync(session.AccessToken, session.ExpiresAt);
                return true;
            }

            return false;
        }
        finally
        {
            _running = null;
        }
    }
}
