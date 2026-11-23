namespace OneLoaf.Web.Shared.Auth;

/// <summary>
/// Singleton bridge from the HTTP handlers (which run in their own DI scope) to the app: a request that carried a token
/// got 401, so the session has ended. <see cref="SessionMonitor"/> listens and dispatches <see cref="SessionExpired"/>.
/// </summary>
public sealed class AuthEvents
{
    /// <summary>Raised when an authenticated request was answered with 401.</summary>
    public event EventHandler? Unauthorized;

    /// <summary>Reports a 401 to an authenticated request.</summary>
    public void RaiseUnauthorized() => Unauthorized?.Invoke(this, EventArgs.Empty);
}
