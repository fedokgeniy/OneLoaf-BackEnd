using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>Home, new batch, batch, calendar and starters on recorded API answers at phone and desktop width (US-20–31, US-43).</summary>
public sealed class ProductionScreensTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");

    [Theory]
    [InlineData(360, 640)]
    [InlineData(1440, 900)]
    public async Task ProductionScreens_RecordedData_Render(int width, int height)
    {
        Assert.SkipWhen(WebAppFixture.WebRoot is null, $"Set {WebAppFixture.WebRootVariable} to the published wwwroot.");
        await using var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = app.BaseAddress.ToString(),
            ViewportSize = new ViewportSize { Width = width, Height = height },
        });
        var page = await context.NewPageAsync();
        var (batchStatus, batch) = RecordedApi.Case("core-api", "GetBatch", "typical");
        var batchId = (string)batch!["id"]!;

        // Playwright tries the most recently registered route first: generic patterns before specific ones.
        await Route(page, "**/api/v1/auth/login", "core-api", "Login", "typical", login: true);
        await Route(page, "**/api/v1/batches?*", "core-api", "ListBatches", "calendar range");
        await Route(page, "**/api/v1/recipes?*", "core-api", "ListRecipes", "typical");
        await Route(page, "**/api/v1/starters?*", "core-api", "ListStarters", "typical");
        await Route(page, "**/api/v1/starters/*/feedings?*", "core-api", "ListFeedings", "typical");
        await Route(page, "**/api/v1/flours?*", "core-api", "ListFlours", "typical");
        await Route(page, "**/api/v1/bake-records?*", "core-api", "ListBakeRecords", "typical");
        await Route(page, "**/api/v1/reminders?*", "notification", "ListReminders", "upcoming");
        await Route(page, "**/api/v1/batches/preview", "core-api", "PreviewBatch", "typical");
        await Route(page, $"**/api/v1/batches/{batchId}/kneading-plan", "core-api", "GetKneadingPlan", "typical");
        await RecordedApi.RouteAsync(page, $"**/api/v1/batches/{batchId}", batchStatus, batch);

        await page.GotoAsync("/login");
        await page.GetByLabel("E-mail").FillAsync("docs.baker.f71fcafd8@example.com");
        await page.GetByLabel("Password").FillAsync("Rye-Bread-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Recent bakes" })).ToBeVisibleAsync();
        await Shot(page, "home", width);

        await page.GotoAsync("/batches/new");
        await page.GetByLabel("Recipe 1").SelectOptionAsync(new SelectOptionValue { Index = 1 });
        await Assertions.Expect(page.Locator(".new-batch__preview .ol-stats")).ToBeVisibleAsync();
        await Shot(page, "new-batch", width);

        await page.GotoAsync($"/batches/{batchId}");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "All stages" })).ToBeVisibleAsync();
        await Shot(page, "batch", width);

        await page.GotoAsync("/batches?week=2026-W48");
        await Assertions.Expect(page.Locator(".calendar__batch").First).ToBeVisibleAsync();
        await Shot(page, "calendar", width);

        await page.GotoAsync("/starters");
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Log feeding" }).First).ToBeVisibleAsync();
        await Shot(page, "starters", width);
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
