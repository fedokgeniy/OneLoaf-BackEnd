using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.CoreApi.Infrastructure.Auth;
using OneLoaf.CoreApi.RateLimiting;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Errors;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>Registration, login, session refresh and logout (US-01, US-02). Rate limited per client IP.</summary>
public static class AuthEndpoints
{
    /// <summary>Route of the group.</summary>
    public const string GroupRoute = Routes.ApiV1 + "/auth";

    /// <summary>Generic message for a failed login: the same for an unknown e-mail and a wrong password (US-02).</summary>
    public const string InvalidCredentialsTitle = "Invalid e-mail or password.";

    /// <summary>Name of the refresh token cookie.</summary>
    public const string RefreshCookieName = "oneloaf_refresh";

    /// <summary>Message when the refresh token cannot be exchanged.</summary>
    public const string SessionEndedTitle = "The session has ended. Sign in again.";

    /// <summary>Maps the endpoints.</summary>
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(GroupRoute)
            .WithTags(ApiTags.Auth)
            .RequireRateLimiting(RateLimitPolicies.Auth)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .WithValidation<RegisterRequest>()
            .WithName("Register")
            .WithSummary("Register a new account")
            .WithDescription("US-01. Creates the user and an empty workspace (kitchen settings with defaults).")
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithValidation<LoginRequest>()
            .WithName("Login")
            .WithSummary("Log in and get an access token")
            .WithDescription("US-02. Returns an RS256 JWT and sets the refresh token as an HttpOnly cookie. Unknown e-mail " +
                             "and wrong password give the same 401.");

        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshSession")
            .WithSummary("Exchange the refresh token cookie for a new access token")
            .WithDescription("US-02. The cookie is replaced by the next token of the same sign-in (rotation). 401 for a " +
                             "missing, expired or revoked token; a token that was already exchanged revokes the whole sign-in.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .WithName("Logout")
            .WithSummary("Log out")
            .WithDescription("US-02. Revokes the refresh tokens of this sign-in and removes the cookie; the access token " +
                             "expires by itself and the client discards it.");

        return app;
    }

    private static async Task<Created<RegisterResponse>> RegisterAsync(
        RegisterRequest request, AuthService auth, CancellationToken cancellationToken)
    {
        var response = await auth.RegisterAsync(request, cancellationToken);
        return TypedResults.Created(MeEndpoints.GroupRoute, response);
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, AuthService auth, HttpContext http, IOptions<JwtIssuerOptions> options,
        CancellationToken cancellationToken)
    {
        var tokens = await auth.LoginAsync(request, cancellationToken);
        if (tokens is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: InvalidCredentialsTitle,
                type: ProblemTypes.Unauthorized);
        }

        SetRefreshCookie(http, options.Value, tokens.RefreshToken);
        return TypedResults.Ok(tokens.Response);
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> RefreshAsync(
        AuthService auth, HttpContext http, IOptions<JwtIssuerOptions> options, CancellationToken cancellationToken)
    {
        var tokens = await auth.RefreshAsync(http.Request.Cookies[RefreshCookieName], cancellationToken);
        if (tokens is null)
        {
            http.Response.Cookies.Delete(RefreshCookieName, RefreshCookie(http, options.Value, null));
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: SessionEndedTitle,
                type: ProblemTypes.Unauthorized);
        }

        SetRefreshCookie(http, options.Value, tokens.RefreshToken);
        return TypedResults.Ok(tokens.Response);
    }

    private static async Task<NoContent> LogoutAsync(
        AuthService auth, HttpContext http, IOptions<JwtIssuerOptions> options, CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(http.Request.Cookies[RefreshCookieName], cancellationToken);
        http.Response.Cookies.Delete(RefreshCookieName, RefreshCookie(http, options.Value, null));
        return TypedResults.NoContent();
    }

    private static void SetRefreshCookie(HttpContext http, JwtIssuerOptions options, IssuedRefreshToken token) =>
        http.Response.Cookies.Append(RefreshCookieName, token.Value, RefreshCookie(http, options, token.ExpiresAt));

    /// <summary>
    /// The cookie is not readable by scripts (HttpOnly), is sent only to the auth endpoints of this site
    /// (path, SameSite=Strict) and only over HTTPS when the site is served over HTTPS.
    /// </summary>
    private static CookieOptions RefreshCookie(HttpContext http, JwtIssuerOptions options, DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = http.Request.IsHttps,
        SameSite = SameSiteMode.Strict,
        Path = options.RefreshCookiePath,
        Expires = expires,
        IsEssential = true,
    };
}
