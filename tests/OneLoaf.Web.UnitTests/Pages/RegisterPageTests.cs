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

public sealed class RegisterPageTests : BunitContext
{
    [Fact]
    public void Submit_ServerValidationErrors_AppearUnderTheirFields()
    {
        // Recorded case "invalid fields" of docs/api/examples/core-api/Register.json (the client checks are passed).
        var cut = RenderRegister(StubHttpHandler.Json(HttpStatusCode.BadRequest, """
            {"type":"https://oneloaf.app/problems/validation","title":"One or more validation errors occurred.","status":400,
             "errors":{"displayName":["'Display Name' must not be blank."],"captcha":["Unknown field."]}}
            """, "application/problem+json"));

        FillValid(cut);

        cut.WaitForAssertion(() =>
            Assert.Contains("'Display Name' must not be blank.", cut.Find(".ol-field__error").TextContent));
        Assert.Contains("Unknown field.", cut.Find(".ol-alert").TextContent);
    }

    [Fact]
    public void Submit_EmailTaken_ShowsConflictUnderEmail()
    {
        var cut = RenderRegister(StubHttpHandler.Json(HttpStatusCode.Conflict, """
            {"type":"https://oneloaf.app/problems/conflict","title":"The request conflicts with existing data.","status":409,
             "detail":"An account with this e-mail already exists."}
            """, "application/problem+json"));

        FillValid(cut);

        cut.WaitForAssertion(() =>
        {
            var email = cut.Find("input[type=email]");
            Assert.Equal("true", email.GetAttribute("aria-invalid"));
            Assert.Contains("An account with this e-mail already exists.", cut.Find(".ol-field__error").TextContent);
        });
    }

    [Fact]
    public void Submit_TermsNotAccepted_BlocksSubmit()
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.Created, """{"userId":"99cf6d64-ee83-4a4b-9f20-8cb359c15b6a"}""");
        var cut = RenderRegister(stub);

        cut.FindAll("input.ol-input")[0].Change("Docs Baker");
        cut.Find("input[type=email]").Change("baker@example.com");
        cut.Find("input[type=password]").Change("Rye-Bread-2026!");
        cut.Find("form").Submit();

        Assert.Contains("Accept the terms", cut.Find(".ol-field__error").TextContent);
        Assert.Null(stub.LastRequest);
    }

    private IRenderedComponent<RegisterPage> RenderRegister(StubHttpHandler stub)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<AuthEvents>();
        Services.AddSingleton<TokenStore>();
        Services.AddSingleton(PipelineFactory.Json);
        Services.AddSingleton<IHttpClientFactory>(new StubClients(stub));
        Services.AddSingleton(NullLogger<ApiClient>.Instance);
        Services.AddScoped<ApiClientFactory>();
        Services.AddScoped<AccountApi>();
        Services.AddScoped<OneLoaf.Web.Account.SessionRefresher>();
        Services.AddScoped<DeviceContext>();
        Services.AddFluxor(o => o.ScanAssemblies(typeof(RegisterPage).Assembly, typeof(SessionState).Assembly));
        Services.GetRequiredService<NavigationManager>().NavigateTo("register");
        Render<Fluxor.Blazor.Web.StoreInitializer>();
        return Render<RegisterPage>();
    }

    private static void FillValid(IRenderedComponent<RegisterPage> cut)
    {
        cut.FindAll("input.ol-input")[0].Change("Docs Baker");
        cut.Find("input[type=email]").Change("baker@example.com");
        cut.Find("input[type=password]").Change("Rye-Bread-2026!");
        cut.Find("input[type=checkbox]").Change(true);
        cut.Find("form").Submit();
    }

    private sealed class StubClients(StubHttpHandler stub) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(new ProblemDetailsOnly(stub), disposeHandler: false) { BaseAddress = new Uri("http://localhost:5080/") };
    }

    /// <summary>The real ProblemDetailsHandler over the stub, so field errors reach the page.</summary>
    private sealed class ProblemDetailsOnly : DelegatingHandler
    {
        public ProblemDetailsOnly(StubHttpHandler stub)
        {
            var problems = new OneLoaf.Web.Shared.Api.Handlers.ProblemDetailsHandler(new AuthEvents(), PipelineFactory.Json, TimeProvider.System)
            {
                InnerHandler = stub,
            };
            InnerHandler = problems;
        }
    }
}
