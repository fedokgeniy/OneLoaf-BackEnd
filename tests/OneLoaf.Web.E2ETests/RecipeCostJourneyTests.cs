using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>
/// Sign in → recipes → "Country sourdough" → 3 loaves → cost (testing.md §3): the API is asked for loaves=3 and the
/// page shows the recorded total, electricity, packaging and loss lines.
/// </summary>
public sealed partial class RecipeCostJourneyTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");

    [Theory]
    [InlineData(360, 640)]
    [InlineData(768, 1024)]
    [InlineData(1440, 900)]
    public async Task RecipeCost_ThreeLoaves_ShowsRecordedTotalAndLines(int width, int height)
    {
        Assert.SkipWhen(WebAppFixture.WebRoot is null, $"Set {WebAppFixture.WebRootVariable} to the published wwwroot.");
        await using var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = app.BaseAddress.ToString(),
            ViewportSize = new ViewportSize { Width = width, Height = height },
        });
        var page = await context.NewPageAsync();
        await RouteRecorded(page);

        await page.GotoAsync("/login?returnUrl=%2Frecipes");
        await page.GetByLabel("E-mail").FillAsync("docs.baker.f71fcafd8@example.com");
        await page.GetByLabel("Password").FillAsync("Rye-Bread-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await page.GetByRole(AriaRole.Link, new() { Name = "Country sourdough" }).ClickAsync();

        await Assertions.Expect(page).ToHaveTitleAsync("Country sourdough · OneLoaf");
        var loaves = page.GetByLabel("Loaves", new() { Exact = true });
        await loaves.FillAsync("3");
        // The loaves change is debounced; wait for the cost request it causes.
        await page.RunAndWaitForRequestAsync(
            () => loaves.PressAsync("Tab"),
            r => r.Url.Contains("/cost?", StringComparison.Ordinal) && LoavesThree().IsMatch(r.Url));

        var (_, cost) = RecordedApi.Case("core-api", "GetRecipeCost", "typical");
        var total = ((decimal)cost!["total"]!).ToString("0.00", CultureInfo.InvariantCulture) + " EUR";
        var table = page.Locator("#recipe-panel-cost table");
        await Assertions.Expect(page.Locator(".ol-stats")).ToContainTextAsync(total);
        await Assertions.Expect(table).ToContainTextAsync("Electricity");
        await Assertions.Expect(table).ToContainTextAsync("Packaging");
        await Assertions.Expect(table).ToContainTextAsync("Loss");

        Directory.CreateDirectory(ScreenshotDir);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(ScreenshotDir, $"recipe-cost-{width}.png"), FullPage = true });
    }

    private static async Task RouteRecorded(IPage page)
    {
        var (loginStatus, login) = RecordedApi.Case("core-api", "Login", "typical");
        await RecordedApi.RouteAsync(page, "**/api/v1/auth/login", loginStatus, RecordedApi.WithReadableToken(login!));
        foreach (var (pattern, operation) in new[]
        {
            ("**/api/v1/recipes/*/scaled*", "ScaleRecipe"),
            ("**/api/v1/recipes/*/cost*", "GetRecipeCost"),
            ("**/api/v1/recipes/*/production-figures*", "GetProductionFigures"),
            ("**/api/v1/recipes/*/timeline*", "GetRecipeTimeline"),
        })
        {
            var (status, body) = RecordedApi.Case("core-api", operation, "typical");
            await RecordedApi.RouteAsync(page, pattern, status, body);
        }

        var (listStatus, list) = RecordedApi.Case("core-api", "ListRecipes", "typical");
        var (recipeStatus, recipe) = RecordedApi.Case("core-api", "GetRecipe", "typical");
        // One handler for the list and the recipe: both start with /api/v1/recipes; registered last, so it is tried first.
        await page.RouteAsync(RecipeOrList(), route => route.Request.Url.Contains("/recipes?", StringComparison.Ordinal)
            ? route.FulfillAsync(new RouteFulfillOptions { Status = listStatus, ContentType = "application/json", Body = list!.ToJsonString() })
            : route.FulfillAsync(new RouteFulfillOptions { Status = recipeStatus, ContentType = "application/json", Body = recipe!.ToJsonString() }));
    }

    [GeneratedRegex(@"/api/v1/recipes(\?|/[0-9a-f-]{36}$)")]
    private static partial Regex RecipeOrList();

    [GeneratedRegex(@"[?&]loaves=3(&|$)")]
    private static partial Regex LoavesThree();
}
