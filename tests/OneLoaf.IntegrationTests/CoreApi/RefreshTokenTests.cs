using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

/// <summary>Session refresh end to end (US-02): the HttpOnly cookie, rotation, reuse detection and sign-out.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class RefreshTokenTests(ApiFixture fixture)
{
    private const string Login = "/api/v1/auth/login";
    private const string Refresh = "/api/v1/auth/refresh";
    private const string Logout = "/api/v1/auth/logout";
    private const string CookieName = "oneloaf_refresh";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task US02_Refresh_RotatesTheCookieAndAcceptsASecondTabAtOnce()
    {
        using var client = fixture.Api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var user = await fixture.Api.RegisterAsync();

        using var login = await client.PostAsJsonAsync(Login, new LoginRequest(user.Email, user.Password), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var setCookie = Assert.Single(login.Headers.GetValues("Set-Cookie"), c => c.StartsWith(CookieName + "=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        var first = CookieValue(setCookie);

        // The body never carries the refresh token: scripts cannot read it.
        Assert.DoesNotContain(first, await login.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);

        using var refreshed = await SendAsync(client, Refresh, first);
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var body = await refreshed.Content.ReadFromJsonAsync<LoginResponse>(CoreApiFactory.Json, Ct);
        Assert.False(string.IsNullOrEmpty(body!.AccessToken));
        Assert.Equal(user.Email, body.User.Email);
        var second = CookieValue(refreshed.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal)));
        Assert.NotEqual(first, second);

        // A second tab of the same browser exchanges the same cookie a moment later: the sign-in continues. A use after
        // the grace period revokes the whole sign-in (AuthServiceTests, with a clock).
        using var secondTab = await SendAsync(client, Refresh, first);
        Assert.Equal(HttpStatusCode.OK, secondTab.StatusCode);
        using var current = await SendAsync(client, Refresh, second);
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
    }

    [Fact]
    public async Task US02_Logout_RevokesTheRefreshToken()
    {
        using var client = fixture.Api.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var user = await fixture.Api.RegisterAsync();
        using var login = await client.PostAsJsonAsync(Login, new LoginRequest(user.Email, user.Password), CoreApiFactory.Json, Ct);
        var token = CookieValue(login.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(CookieName + "=", StringComparison.Ordinal)));

        using var logout = await SendAsync(client, Logout, token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        using var refresh = await SendAsync(client, Refresh, token);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        using var withoutCookie = await client.PostAsync(Refresh, null, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, withoutCookie.StatusCode);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"{CookieName}={refreshToken}");
        return await client.SendAsync(request, Ct);
    }

    private static string CookieValue(string setCookie) => setCookie.Split(';')[0][(CookieName.Length + 1)..];
}
