using Microsoft.AspNetCore.Http.HttpResults;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.CoreApi.RateLimiting;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Errors;
using OneLoaf.ServiceDefaults.Validation;

namespace OneLoaf.CoreApi.Endpoints;

/// <summary>E1: registration and login (US-01, US-02). Rate limited per client IP.</summary>
public static class AuthEndpoints
{
    /// <summary>Route of the group.</summary>
    public const string GroupRoute = Routes.ApiV1 + "/auth";

    /// <summary>Generic message for a failed login: the same for an unknown e-mail and a wrong password (US-02).</summary>
    public const string InvalidCredentialsTitle = "Invalid e-mail or password.";

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
            .WithDescription("US-02. Returns an RS256 JWT. Unknown e-mail and wrong password give the same 401.");

        group.MapPost("/logout", Logout)
            .RequireAuthorization()
            .WithName("Logout")
            .WithSummary("Log out")
            .WithDescription("US-02. Access tokens are short-lived and stateless; the client discards its token.")
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<Created<RegisterResponse>> RegisterAsync(
        RegisterRequest request, AuthService auth, CancellationToken cancellationToken)
    {
        var response = await auth.RegisterAsync(request, cancellationToken);
        return TypedResults.Created(MeEndpoints.GroupRoute, response);
    }

    private static async Task<Results<Ok<LoginResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request, AuthService auth, CancellationToken cancellationToken)
    {
        var response = await auth.LoginAsync(request, cancellationToken);
        return response is null
            ? TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: InvalidCredentialsTitle,
                type: ProblemTypes.Unauthorized)
            : TypedResults.Ok(response);
    }

    private static NoContent Logout() => TypedResults.NoContent();
}
