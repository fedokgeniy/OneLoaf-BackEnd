using Fluxor;
using OneLoaf.Web.Shared.Api;

namespace OneLoaf.Web.Shared.Auth;

/// <summary>Where the session is.</summary>
public enum SessionStatus
{
    /// <summary>Nobody is signed in.</summary>
    Anonymous,

    /// <summary>A sign-in or registration request is running.</summary>
    Busy,

    /// <summary>A valid token is held.</summary>
    SignedIn,
}

/// <summary>Global session slice: the user, token expiry and the last sign-in/registration error.</summary>
[FeatureState(Name = "Session")]
public sealed record SessionState(SessionStatus Status, SessionUser? User, DateTimeOffset? ExpiresAt, ApiError? Error)
{
    private SessionState()
        : this(SessionStatus.Anonymous, null, null, null)
    {
    }

    /// <summary>The initial, signed-out state.</summary>
    public static SessionState Initial { get; } = new();
}

/// <summary>Intent: sign in with e-mail and password.</summary>
public sealed record Login(string Email, string Password);

/// <summary>Intent: create an account, then sign in with it.</summary>
public sealed record Register(string DisplayName, string Email, string Password);

/// <summary>Result: signed in; the token is already in the <see cref="TokenStore"/>.</summary>
public sealed record LoggedIn(SessionUser User, DateTimeOffset ExpiresAt);

/// <summary>Result: a token of this tab was restored after a reload.</summary>
public sealed record SessionRestored(SessionUser User, DateTimeOffset ExpiresAt);

/// <summary>Result: sign-in failed.</summary>
public sealed record LoginFailed(ApiError Error);

/// <summary>Result: registration failed.</summary>
public sealed record RegisterFailed(ApiError Error);

/// <summary>Intent: sign out.</summary>
public sealed record Logout;

/// <summary>Result: signed out, token dropped.</summary>
public sealed record LoggedOut;

/// <summary>Intent: get a new access token before the current one ends, or after a 401.</summary>
public sealed record RefreshSession;

/// <summary>The token expired or an authenticated request got 401.</summary>
public sealed record SessionExpired;

/// <summary>Pure reducers of <see cref="SessionState"/>.</summary>
public static class SessionReducers
{
    /// <summary>Sign-in starts.</summary>
    [ReducerMethod(typeof(Login))]
    public static SessionState OnLogin(SessionState state) => state with { Status = SessionStatus.Busy, Error = null };

    /// <summary>Registration starts.</summary>
    [ReducerMethod(typeof(Register))]
    public static SessionState OnRegister(SessionState state) => state with { Status = SessionStatus.Busy, Error = null };

    /// <summary>Signed in.</summary>
    [ReducerMethod]
    public static SessionState OnLoggedIn(SessionState state, LoggedIn action) =>
        new(SessionStatus.SignedIn, action.User, action.ExpiresAt, null);

    /// <summary>Session restored after a reload.</summary>
    [ReducerMethod]
    public static SessionState OnRestored(SessionState state, SessionRestored action) =>
        new(SessionStatus.SignedIn, action.User, action.ExpiresAt, null);

    /// <summary>Sign-in failed.</summary>
    [ReducerMethod]
    public static SessionState OnLoginFailed(SessionState state, LoginFailed action) =>
        SessionState.Initial with { Error = action.Error };

    /// <summary>Registration failed.</summary>
    [ReducerMethod]
    public static SessionState OnRegisterFailed(SessionState state, RegisterFailed action) =>
        SessionState.Initial with { Error = action.Error };

    /// <summary>Signed out.</summary>
    [ReducerMethod(typeof(LoggedOut))]
    public static SessionState OnLoggedOut(SessionState state) => SessionState.Initial;

    /// <summary>Session ended.</summary>
    [ReducerMethod(typeof(SessionExpired))]
    public static SessionState OnExpired(SessionState state) => SessionState.Initial;
}
