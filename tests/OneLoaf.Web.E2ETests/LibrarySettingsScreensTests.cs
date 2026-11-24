using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>Public library signed out, then settings signed in, on recorded API answers (US-04, US-29, US-40, US-50).</summary>
public sealed class LibrarySettingsScreensTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");

    [Theory]
    [InlineData(360, 640)]
    [InlineData(1440, 900)]
    public async Task LibraryAndSettings_RecordedData_Render(int width, int height)
    {
        Assert.SkipWhen(WebAppFixture.WebRoot is null, $"Set {WebAppFixture.WebRootVariable} to the published wwwroot.");
        await using var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            // Route mocks do not see requests made by the service worker; the app works without it.
            ServiceWorkers = ServiceWorkerPolicy.Block,
            BaseURL = app.BaseAddress.ToString(),
            ViewportSize = new ViewportSize { Width = width, Height = height },
        });
        var page = await context.NewPageAsync();
        var (publicationStatus, publication) = RecordedApi.Case("core-api", "GetPublication", "anonymous");
        var publicationId = (string)publication!["id"]!;

        // Playwright tries the most recently registered route first: generic patterns before specific ones.
        await Route(page, "**/api/v1/auth/login", "core-api", "Login", "typical", login: true);
        await Route(page, "**/api/v1/batches?*", "core-api", "ListBatches", "calendar range");
        await Route(page, "**/api/v1/starters?*", "core-api", "ListStarters", "typical");
        await Route(page, "**/api/v1/reminders?*", "notification", "ListReminders", "upcoming");
        await Route(page, "**/api/v1/bake-records?*", "core-api", "ListBakeRecords", "typical");
        await Route(page, "**/api/v1/library?*", "core-api", "SearchLibrary", "anonymous search");
        await Route(page, $"**/api/v1/library/{publicationId}/comments?*", "core-api", "ListComments", "typical");
        await RecordedApi.RouteAsync(page, $"**/api/v1/library/{publicationId}", publicationStatus, publication);

        await page.GotoAsync("/library");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Country sourdough" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Sign in to copy and comment")).ToBeVisibleAsync();
        await Shot(page, "library", width);
        await Accessibility.AssertNoSeriousViolationsAsync(page, "library");

        await page.GetByRole(AriaRole.Link, new() { Name = "Country sourdough" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Ingredients" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Baked it on Sunday, lovely crumb!")).ToBeVisibleAsync();
        await Shot(page, "library-detail", width);
        await Accessibility.AssertNoSeriousViolationsAsync(page, "library-detail");

        await page.GotoAsync("/login");
        await page.GetByLabel("E-mail").FillAsync("docs.baker.f71fcafd8@example.com");
        await page.GetByLabel("Password").FillAsync("Rye-Bread-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Recent bakes" })).ToBeVisibleAsync();

        await page.GotoAsync("/settings");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Display" })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Dark" }).ClickAsync();
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("data-theme", "dark");
        await Shot(page, "settings-dark", width);
        await Accessibility.AssertNoSeriousViolationsAsync(page, "settings-dark");
    }

    private static async Task Route(IPage page, string pattern, string service, string operation, string caseName, bool login = false)
    {
        var (status, body) = RecordedApi.Case(service, operation, caseName);
        await RecordedApi.RouteAsync(page, pattern, status, login ? RecordedApi.WithReadableToken(body!) : body);
    }

    private static async Task Shot(IPage page, string name, int width)
    {
        Directory.CreateDirectory(ScreenshotDir);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(ScreenshotDir, $"{name}-{width}.png"), FullPage = true });
    }
}
