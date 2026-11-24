using System.Net;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.Web.Account;
using OneLoaf.Web.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Device;
using OneLoaf.Web.UnitTests.Api;

namespace OneLoaf.Web.UnitTests.Pages;

public sealed class LoginPageTests : BunitContext
{
    private const string LoginOk = """
        {"accessToken":"eyJhbGciOiJSUzI1NiJ9.e30.sig","expiresAt":"2099-01-01T00:00:00+00:00",
         "user":{"id":"99cf6d64-ee83-4a4b-9f20-8cb359c15b6a","email":"baker@example.com","displayName":"Docs Baker",
                 "role":"user","createdAt":"2026-11-23T10:08:53+00:00","lastLoginAt":null}}
        """;

    private const string WrongPassword = """
        {"type":"https://oneloaf.app/problems/unauthorized","title":"Invalid e-mail or password.","status":401,
         "correlationId":"23c18f8b8c9449d7863f94ebd1b9d0a5"}
        """;

    [Fact]
    public void Submit_WrongPassword_ShowsOneGenericMessage()
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.Unauthorized, WrongPassword, "application/problem+json");
        var cut = RenderLogin(stub);

        Fill(cut, "baker@example.com", "Wrong-Password-1");

        cut.WaitForAssertion(() => Assert.Equal("E-mail or password is wrong.", cut.Find(".ol-alert__body").TextContent.Trim()));
        Assert.EndsWith("/login?returnUrl=%2Frecipes", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void Submit_ValidCredentials_StoresTokenAndNavigatesToReturnUrl()
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.OK, LoginOk);
        var cut = RenderLogin(stub);

        Fill(cut, "baker@example.com", "Rye-Bread-2026!");

        var navigation = Services.GetRequiredService<NavigationManager>();
        cut.WaitForAssertion(() => Assert.Equal("http://localhost/recipes", navigation.Uri));
        Assert.Equal("eyJhbGciOiJSUzI1NiJ9.e30.sig", Services.GetRequiredService<TokenStore>().ValidToken);
        Assert.Equal(SessionStatus.SignedIn, Services.GetRequiredService<IState<SessionState>>().Value.Status);
        Assert.Equal("http://localhost:5080/api/v1/auth/login", stub.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public void Submit_EmptyFields_ShowsValidationAndSendsNothing()
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.OK, LoginOk);
        var cut = RenderLogin(stub);

        cut.Find("form").Submit();

        Assert.Equal(2, cut.FindAll(".ol-field__error").Count);
        Assert.Null(stub.LastRequest);
    }

    private IRenderedComponent<LoginPage> RenderLogin(StubHttpHandler stub)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<AuthEvents>();
        Services.AddSingleton<TokenStore>();
        Services.AddSingleton(PipelineFactory.Json);
        Services.AddSingleton<IHttpClientFactory>(new StubHttpClientFactory(stub));
        Services.AddSingleton(NullLogger<ApiClient>.Instance);
        Services.AddScoped<ApiClientFactory>();
        Services.AddScoped<AccountApi>();
        Services.AddScoped<OneLoaf.Web.Account.SessionRefresher>();
        Services.AddScoped<DeviceContext>();
        Services.AddFluxor(o => o.ScanAssemblies(typeof(LoginPage).Assembly, typeof(SessionState).Assembly));

        Services.GetRequiredService<NavigationManager>().NavigateTo("login?returnUrl=%2Frecipes");
        Render<Fluxor.Blazor.Web.StoreInitializer>();
        return Render<LoginPage>();
    }

    private static void Fill(IRenderedComponent<LoginPage> cut, string email, string password)
    {
        cut.Find("input[type=email]").Change(email);
        cut.Find("input[type=password]").Change(password);
        cut.Find("form").Submit();
    }

    private sealed class StubHttpClientFactory(StubHttpHandler stub) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(stub, disposeHandler: false) { BaseAddress = new Uri("http://localhost:5080/") };
    }
}
