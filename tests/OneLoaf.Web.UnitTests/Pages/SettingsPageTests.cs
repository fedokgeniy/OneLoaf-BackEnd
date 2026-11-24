using System.Net;
using Fluxor;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.Web.Account;
using OneLoaf.Web.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts.Notification;
using OneLoaf.Web.Shared.Device;
using OneLoaf.Web.Shared.State.Display;
using OneLoaf.Web.UnitTests.Api;

namespace OneLoaf.Web.UnitTests.Pages;

public sealed class SettingsPageTests : BunitContext
{
    private readonly FakeSettings _settings = new();

    public SettingsPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TimeProvider.System);
        Services.AddSingleton<AuthEvents>();
        Services.AddSingleton<TokenStore>();
        Services.AddSingleton(PipelineFactory.Json);
        Services.AddSingleton<IHttpClientFactory>(new Clients(StubHttpHandler.Json(HttpStatusCode.NoContent, string.Empty)));
        Services.AddSingleton(NullLogger<ApiClient>.Instance);
        Services.AddScoped<ApiClientFactory>();
        Services.AddScoped<AccountApi>();
        Services.AddScoped<DeviceContext>();
        Services.AddSingleton<ISettingsService>(_settings);
        Services.AddFluxor(o => o.ScanAssemblies(typeof(SettingsPage).Assembly, typeof(SessionState).Assembly));
        Render<Fluxor.Blazor.Web.StoreInitializer>();
    }

    [Fact]
    public void Theme_Dark_UpdatesTheDisplayPreferences()
    {
        var cut = Render<SettingsPage>();

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Dark").Click();

        Assert.Equal(ThemePreference.Dark, Services.GetRequiredService<IState<DisplayState>>().Value.Preferences.Theme);
    }

    [Fact]
    public void DeleteAccount_WrongPassword_ShowsTheErrorAndStaysSignedIn()
    {
        _settings.DeleteResult = ApiResult.Fail(ApiError.FromStatus(403) with { Detail = "The password is incorrect." });
        var cut = Render<SettingsPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Delete account", StringComparison.Ordinal)).Click();
        cut.Find("form#delete-form").Submit();
        Assert.Contains("Enter your password", cut.Markup, StringComparison.Ordinal);
        Assert.Null(_settings.DeletedWith);

        cut.Find("#delete-password").Input("Not-My-Password-1");
        cut.Find("form#delete-form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("The password is incorrect.", cut.Find("#delete-error").TextContent, StringComparison.Ordinal));
        Assert.Equal("Not-My-Password-1", _settings.DeletedWith);
        Assert.DoesNotContain("/login", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    [Fact]
    public void DeleteAccount_Confirmed_SignsOut()
    {
        var cut = Render<SettingsPage>();

        cut.FindAll("button").First(b => b.TextContent.Contains("Delete account", StringComparison.Ordinal)).Click();
        cut.Find("#delete-password").Input("Rye-Bread-2026!");
        cut.Find("form#delete-form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/login", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal));
    }

    private sealed class Clients(StubHttpHandler stub) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(stub, disposeHandler: false) { BaseAddress = new Uri("http://localhost:5080/") };
    }

    private sealed class FakeSettings : ISettingsService
    {
        public ApiResult DeleteResult { get; set; } = ApiResult.Ok;

        public string? DeletedWith { get; private set; }

        public Task<ApiResult> DeleteAccountAsync(string password, CancellationToken ct = default)
        {
            DeletedWith = password;
            return Task.FromResult(DeleteResult);
        }

        public Task<ApiResult> ExportAsync(string path, string fileName, CancellationToken ct = default) => Task.FromResult(ApiResult.Ok);

        public Task<PushStatus> GetPushStatusAsync() => Task.FromResult(PushStatus.Off);

        public Task<(PushStatus Status, ApiError? Error)> EnablePushAsync() => Task.FromResult<(PushStatus, ApiError?)>((PushStatus.On, null));

        public Task<ApiError?> DisablePushAsync() => Task.FromResult<ApiError?>(null);

        public Task<ApiResult<TestPushResponse>> SendTestPushAsync() =>
            Task.FromResult<ApiResult<TestPushResponse>>(new ApiResult<TestPushResponse>.Success(new TestPushResponse(true)));
    }
}
