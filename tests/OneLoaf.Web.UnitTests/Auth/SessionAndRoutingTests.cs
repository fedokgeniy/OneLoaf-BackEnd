using System.Buffers.Text;
using System.Text;
using OneLoaf.Web.Layout;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;

namespace OneLoaf.Web.UnitTests.Auth;

public sealed class SessionAndRoutingTests
{
    [Theory]
    [InlineData("/recipes/42?tab=cost", "/recipes/42?tab=cost")]
    [InlineData(null, "/")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("/login?returnUrl=%2F", "/")]
    public void Sanitize_Url_AllowsOnlyLocalPaths(string? url, string expected) =>
        Assert.Equal(expected, ReturnUrl.Sanitize(url));

    [Fact]
    public void LoginUrl_ProtectedPath_EncodesReturnUrl() =>
        Assert.Equal("login?returnUrl=%2Fbatches%2Fnew", ReturnUrl.LoginUrl("/batches/new"));

    [Fact]
    public void ToPrincipal_CoreApiToken_ReadsUserClaims()
    {
        var payload = """{"sub":"99cf6d64-ee83-4a4b-9f20-8cb359c15b6a","email":"baker@example.com","name":"Docs Baker","role":"admin","exp":4102444800}""";
        var token = $"e30.{Base64Url.EncodeToString(Encoding.UTF8.GetBytes(payload))}.sig";

        var user = SessionUser.From(JwtClaims.ToPrincipal(token));

        Assert.NotNull(user);
        Assert.Equal("Docs Baker", user.DisplayName);
        Assert.Equal("baker@example.com", user.Email);
        Assert.True(user.IsAdmin);
    }

    [Theory]
    [InlineData("not-a-jwt")]
    [InlineData("a.%%%.c")]
    public void ToPrincipal_UnreadableToken_IsAnonymous(string token) =>
        Assert.False(JwtClaims.ToPrincipal(token).Identity!.IsAuthenticated);

    [Fact]
    public void Reducers_LoginFlow_MovesThroughStatuses()
    {
        var user = new SessionUser(Guid.NewGuid(), "baker@example.com", "Baker", "user");
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        var error = ApiError.FromStatus(401);

        var busy = SessionReducers.OnLogin(SessionState.Initial);
        var failed = SessionReducers.OnLoginFailed(busy, new LoginFailed(error));
        var signedIn = SessionReducers.OnLoggedIn(failed, new LoggedIn(user, expires));
        var expired = SessionReducers.OnExpired(signedIn);

        Assert.Equal(SessionStatus.Busy, busy.Status);
        Assert.Same(error, failed.Error);
        Assert.Equal(new SessionState(SessionStatus.SignedIn, user, expires, null), signedIn);
        Assert.Equal(SessionState.Initial, expired);
    }
}
