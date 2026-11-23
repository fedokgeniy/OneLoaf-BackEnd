using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>Recipes list (US-05, US-43): recorded list at phone, tablet and desktop width, search goes to the API.</summary>
public sealed class RecipesJourneyTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");

    [Theory]
    [InlineData(360, 640)]
    [InlineData(768, 1024)]
    [InlineData(1440, 900)]
    public async Task Recipes_RecordedList_ShowsCardsAndSearches(int width, int height)
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
        var (loginStatus, login) = RecordedApi.Case("core-api", "Login", "typical");
        await RecordedApi.RouteAsync(page, "**/api/v1/auth/login", loginStatus, RecordedApi.WithReadableToken(login!));
        var (listStatus, list) = RecordedApi.Case("core-api", "ListRecipes", "typical");
        await page.RouteAsync("**/api/v1/recipes?*", route =>
            route.FulfillAsync(new RouteFulfillOptions
            {
                Status = listStatus,
                ContentType = "application/json",
                Body = list!.ToJsonString(),
            }));

        await page.GotoAsync("/login?returnUrl=%2Frecipes");
        await page.GetByLabel("E-mail").FillAsync("docs.baker.f71fcafd8@example.com");
        await page.GetByLabel("Password").FillAsync("Rye-Bread-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        var card = page.GetByRole(AriaRole.Link, new() { Name = "Country sourdough" });
        await Assertions.Expect(card).ToBeVisibleAsync();
        await Assertions.Expect(page).ToHaveTitleAsync("Recipes · OneLoaf");
        Directory.CreateDirectory(ScreenshotDir);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(ScreenshotDir, $"recipes-{width}.png"), FullPage = true });

        await page.RunAndWaitForRequestAsync(
            () => page.GetByRole(AriaRole.Searchbox, new() { Name = "Search recipes" }).FillAsync("country"),
            r => r.Url.Contains("search=country", StringComparison.Ordinal));
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("recipes\\?search=country$"));
    }
}
