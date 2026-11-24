using System.Globalization;
using Microsoft.JSInterop;

namespace OneLoaf.Web.Shared.Auth;

/// <summary>
/// Holds the access token in memory and mirrors it to <c>sessionStorage</c> so that a reload keeps the session; never
/// <c>localStorage</c> (ADR-F07). The refresh token is an HttpOnly cookie this code never sees: an expired access token is simply dropped.
/// </summary>
public sealed class TokenStore(IJSRuntime js, TimeProvider time)
{
    private const string TokenKey = "oneloaf.token";
    private const string ExpiresKey = "oneloaf.token-expires";

    private string? _token;

    /// <summary>Raised after the token was set or cleared.</summary>
    public event EventHandler? Changed;

    /// <summary>Expiry of the current token, or <see langword="null"/> when signed out.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>The token when it exists and has not expired; otherwise <see langword="null"/>.</summary>
    public string? ValidToken => _token is not null && ExpiresAt > time.GetUtcNow() ? _token : null;

    /// <summary>Restores the token of this browser tab from <c>sessionStorage</c>; call once before the app renders.</summary>
    public async Task InitializeAsync()
    {
        try
        {
            var token = await js.InvokeAsync<string?>("sessionStorage.getItem", TokenKey);
            var expires = await js.InvokeAsync<string?>("sessionStorage.getItem", ExpiresKey);
            if (token is not null
                && DateTimeOffset.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at)
                && at > time.GetUtcNow())
            {
                _token = token;
                ExpiresAt = at;
            }
            else
            {
                await RemoveStoredAsync();
            }
        }
        catch (JSException)
        {
            // sessionStorage is unavailable (privacy mode): the session then lives in memory only.
        }
    }

    /// <summary>Stores a new token after sign-in.</summary>
    public async Task SetAsync(string token, DateTimeOffset expiresAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _token = token;
        ExpiresAt = expiresAt;
        try
        {
            await js.InvokeVoidAsync("sessionStorage.setItem", TokenKey, token);
            await js.InvokeVoidAsync("sessionStorage.setItem", ExpiresKey, expiresAt.ToString("O", CultureInfo.InvariantCulture));
        }
        catch (JSException)
        {
            // Memory only, see InitializeAsync.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Drops the token (sign-out, expiry, 401).</summary>
    public async Task ClearAsync()
    {
        var had = _token is not null;
        _token = null;
        ExpiresAt = null;
        await RemoveStoredAsync();
        if (had)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private async Task RemoveStoredAsync()
    {
        try
        {
            await js.InvokeVoidAsync("sessionStorage.removeItem", TokenKey);
            await js.InvokeVoidAsync("sessionStorage.removeItem", ExpiresKey);

            // Recipe data the service worker cached for offline reading belongs to this user (index.html).
            await js.InvokeVoidAsync("oneloafClearUserData");
        }
        catch (JSException)
        {
            // Nothing stored.
        }
    }
}
