using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Account;

/// <summary>Typed client of the account endpoints of the Core API (<c>auth/*</c>, <c>me</c>).</summary>
public sealed class AccountApi(ApiClientFactory api)
{
    /// <summary><c>POST auth/register</c>: 201, 400 validation, 409 e-mail taken.</summary>
    public Task<ApiResult<RegisterResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<RegisterRequest, RegisterResponse>("api/v1/auth/register", request, ct);

    /// <summary>
    /// <c>POST auth/login</c>: 200 with the access token (and the refresh token as an HttpOnly cookie), 401 wrong e-mail
    /// or password, 429 rate limit.
    /// </summary>
    public Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        api.Core.PostWithCookiesAsync<LoginRequest, LoginResponse>("api/v1/auth/login", request, ct);

    /// <summary><c>POST auth/refresh</c>: 200 with a new access token, 401 when the sign-in has ended.</summary>
    public Task<ApiResult<LoginResponse>> RefreshAsync(CancellationToken ct = default) =>
        api.Core.PostEmptyWithCookiesAsync<LoginResponse>("api/v1/auth/refresh", ct);

    /// <summary><c>POST auth/logout</c>: 204; the API revokes the refresh token and removes the cookie.</summary>
    public Task<ApiResult> LogoutAsync(CancellationToken ct = default) =>
        api.Core.PostWithCookiesAsync("api/v1/auth/logout", ct);
}
