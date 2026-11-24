using Fluxor;

namespace OneLoaf.Web.Shared.Auth;

/// <summary>
/// Watches the access token: restores the session after a reload, asks for a new token one minute before expiry and
/// after a 401 from an authenticated request, and ends the session when no new token arrives (ADR-F07). Started
/// once by the app.
/// </summary>
public sealed class SessionMonitor(TokenStore tokens, AuthEvents authEvents, IDispatcher dispatcher, TimeProvider time)
    : IDisposable
{
    /// <summary>How long before expiry a new access token is requested.</summary>
    public static readonly TimeSpan RefreshLead = TimeSpan.FromMinutes(1);

    private ITimer? _refreshTimer;
    private ITimer? _expiryTimer;
    private bool _started;
    private bool _expiredReported;
    private bool _refreshRequested;

    /// <summary>Subscribes to token changes and 401s, and dispatches <see cref="SessionRestored"/> for a kept token.</summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        tokens.Changed += OnTokenChanged;
        authEvents.Unauthorized += OnUnauthorized;
        if (tokens.ValidToken is { } token && tokens.ExpiresAt is { } expiresAt
            && SessionUser.From(JwtClaims.ToPrincipal(token)) is { } user)
        {
            dispatcher.Dispatch(new SessionRestored(user, expiresAt));
        }

        Schedule();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        tokens.Changed -= OnTokenChanged;
        authEvents.Unauthorized -= OnUnauthorized;
        CancelTimers();
    }

    private void OnTokenChanged(object? sender, EventArgs e) => Schedule();

    /// <summary>A 401 with a token: the token was not accepted any more, so one refresh is tried before giving up.</summary>
    private void OnUnauthorized(object? sender, EventArgs e) => RequestRefresh();

    /// <summary>One refresh per access token: several 401s or timers at once ask only once.</summary>
    private void RequestRefresh()
    {
        if (_refreshRequested || _expiredReported)
        {
            return;
        }

        _refreshRequested = true;
        dispatcher.Dispatch(new RefreshSession());
    }

    private void Schedule()
    {
        CancelTimers();
        if (tokens.ValidToken is null || tokens.ExpiresAt is not { } expiresAt)
        {
            return;
        }

        _expiredReported = false;
        _refreshRequested = false;
        var untilExpiry = expiresAt - time.GetUtcNow();
        var untilRefresh = untilExpiry - RefreshLead;
        _refreshTimer = time.CreateTimer(
            _ => RequestRefresh(), null, untilRefresh > TimeSpan.Zero ? untilRefresh : TimeSpan.Zero, Timeout.InfiniteTimeSpan);

        _expiryTimer = time.CreateTimer(
            _ => ReportExpired(), null, untilExpiry > TimeSpan.Zero ? untilExpiry : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private void ReportExpired()
    {
        // Several requests can fail with 401 at once; the user gets one message and one redirect.
        if (_expiredReported)
        {
            return;
        }

        _expiredReported = true;
        CancelTimers();
        dispatcher.Dispatch(new SessionExpired());
    }

    private void CancelTimers()
    {
        _refreshTimer?.Dispose();
        _expiryTimer?.Dispose();
        _refreshTimer = null;
        _expiryTimer = null;
    }
}
