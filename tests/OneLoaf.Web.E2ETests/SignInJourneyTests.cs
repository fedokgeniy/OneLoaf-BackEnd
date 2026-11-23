using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>F0 journey: protected route → sign in → shell on phone and desktop → sign out (US-02, US-43).</summary>
public sealed class SignInJourneyTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");

    [Theory]
    [InlineData(360, 640, "bottom-bar")]
    [InlineData(768, 1024, "rail")]
    [InlineData(1440, 900, "sidebar")]
    public async Task SignIn_RecordedLogin_ShowsShellAndSignsOut(int width, int height, string navigation)
    {
        Assert.SkipWhen(WebAppFixture.WebRoot is null, $"Set {WebAppFixture.WebRootVariable} to the published wwwroot.");
        await using var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = app.BaseAddress.ToString(),
            ViewportSize = new ViewportSize { Width = width, Height = height },
        });
        var page = await context.NewPageAsync();
        var (status, body) = RecordedApi.Case("core-api", "Login", "typical");
        await RecordedApi.RouteAsync(page, "**/api/v1/auth/login", status, RecordedApi.WithReadableToken(body!));
        await RecordedApi.RouteAsync(page, "**/api/v1/auth/logout", 204, null);

        await page.GotoAsync("/");
        await page.WaitForURLAsync("**/login");
        await page.GetByLabel("E-mail").FillAsync("docs.baker.f71fcafd8@example.com");
        await page.GetByLabel("Password").FillAsync("Rye-Bread-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Hello, Docs Baker" })).ToBeVisibleAsync();
        await Assertions.Expect(page).ToHaveTitleAsync("Home · OneLoaf");
        var mainNavigation = page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        await Assertions.Expect(mainNavigation).ToHaveCountAsync(1);
        await Assertions.Expect(mainNavigation.GetByRole(AriaRole.Link, new() { Name = "Home", Exact = true }))
            .ToHaveAttributeAsync("aria-current", "page");
        Directory.CreateDirectory(ScreenshotDir);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(ScreenshotDir, $"home-{navigation}-{width}.png") });

        if (navigation == "bottom-bar")
        {
            await page.GetByRole(AriaRole.Button, new() { Name = "Profile and settings" }).ClickAsync();
        }

        await page.GetByRole(AriaRole.Button, new() { Name = "Sign out" }).ClickAsync();
        await page.WaitForURLAsync("**/login");
        Assert.Null(await page.EvaluateAsync<string?>("sessionStorage.getItem('oneloaf.token')"));
    }

    [Fact]
    public async Task SignIn_WrongPassword_ShowsOneGenericMessage()
    {
        Assert.SkipWhen(WebAppFixture.WebRoot is null, $"Set {WebAppFixture.WebRootVariable} to the published wwwroot.");
        await using var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions { BaseURL = app.BaseAddress.ToString() });
        var page = await context.NewPageAsync();
        var (status, body) = RecordedApi.Case("core-api", "Login", "wrong password");
        await RecordedApi.RouteAsync(page, "**/api/v1/auth/login", status, body);

        await page.GotoAsync("/login");
        await page.GetByLabel("E-mail").FillAsync("docs.baker.f71fcafd8@example.com");
        await page.GetByLabel("Password").FillAsync("Wrong-Password-1");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToHaveTextAsync("E-mail or password is wrong.");
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/login$"));
    }
}
