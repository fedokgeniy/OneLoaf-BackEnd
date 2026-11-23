using Fluxor;

namespace OneLoaf.Web.Shared.Auth;

/// <summary>
/// Watches the token: restores the session after a reload, warns one minute before expiry and ends the session at
/// expiry or on a 401 from an authenticated request (ADR-F07: no refresh token). Started once by the app.
/// </summary>
public sealed class SessionMonitor(TokenStore tokens, AuthEvents authEvents, IDispatcher dispatcher, TimeProvider time)
    : IDisposable
{
    /// <summary>How long before expiry the user is warned.</summary>
    public static readonly TimeSpan WarningLead = TimeSpan.FromMinutes(1);

    private ITimer? _warningTimer;
    private ITimer? _expiryTimer;
    private bool _started;
    private bool _expiredReported;

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

    private void OnUnauthorized(object? sender, EventArgs e) => ReportExpired();

    private void Schedule()
    {
        CancelTimers();
        if (tokens.ValidToken is null || tokens.ExpiresAt is not { } expiresAt)
        {
            return;
        }

        _expiredReported = false;
        var untilExpiry = expiresAt - time.GetUtcNow();
        var untilWarning = untilExpiry - WarningLead;
        if (untilWarning > TimeSpan.Zero)
        {
            _warningTimer = time.CreateTimer(
                _ => dispatcher.Dispatch(new SessionEndingSoon()), null, untilWarning, Timeout.InfiniteTimeSpan);
        }

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
        _warningTimer?.Dispose();
        _expiryTimer?.Dispose();
        _warningTimer = null;
        _expiryTimer = null;
    }
}
