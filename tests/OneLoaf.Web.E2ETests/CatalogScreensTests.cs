using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>Recipe editor, ingredients and kitchen on recorded API answers, at phone and desktop width (US-05, US-10–13, US-43).</summary>
public sealed class CatalogScreensTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");

    [Theory]
    [InlineData(360, 640)]
    [InlineData(1440, 900)]
    public async Task CatalogScreens_RecordedData_RenderAndShowTheirForms(int width, int height)
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
        await Route(page, "**/api/v1/auth/login", "Login", "typical", login: true);
        await Route(page, "**/api/v1/reference/stage-types", "GetStageTypes");
        await Route(page, "**/api/v1/reference/units", "GetUnits");
        await Route(page, "**/api/v1/reference/currencies", "GetCurrencies");
        await Route(page, "**/api/v1/reference/ingredient-categories", "GetIngredientCategories");
        await Route(page, "**/api/v1/flours?*", "ListFlours");
        await Route(page, "**/api/v1/ingredients/*/prices", "GetIngredientPrices", "history with change percent");
        await Route(page, "**/api/v1/ingredients?*", "ListIngredients", "filter by category");
        await Route(page, "**/api/v1/kitchen-settings", "GetKitchenSettings", "defaults after registration");
        var (recipeStatus, recipe) = RecordedApi.Case("core-api", "GetRecipe", "typical");
        await RecordedApi.RouteAsync(page, $"**/api/v1/recipes/{(string)recipe!["id"]!}", recipeStatus, recipe);

        await page.GotoAsync("/login?returnUrl=%2Fingredients");
        await page.GetByLabel("E-mail").FillAsync("docs.baker.f71fcafd8@example.com");
        await page.GetByLabel("Password").FillAsync("Rye-Bread-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Bread flour" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Price history" })).ToBeVisibleAsync();
        await Shot(page, "ingredients", width);
        await Accessibility.AssertNoSeriousViolationsAsync(page, "ingredients");

        await page.GotoAsync($"/recipes/{(string)recipe["id"]!}/edit");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Stages" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Title", new() { Exact = true })).ToHaveValueAsync((string)recipe["title"]!);
        await Shot(page, "recipe-editor", width);
        await Accessibility.AssertNoSeriousViolationsAsync(page, "recipe-editor");

        await page.GotoAsync("/settings/kitchen");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Costs" })).ToBeVisibleAsync();
        await Shot(page, "kitchen", width);
        await Accessibility.AssertNoSeriousViolationsAsync(page, "kitchen");
    }

    private static async Task Route(IPage page, string pattern, string operation, string caseName = "typical", bool login = false)
    {
        var (status, body) = RecordedApi.Case("core-api", operation, caseName);
        await RecordedApi.RouteAsync(page, pattern, status, login ? RecordedApi.WithReadableToken(body!) : body);
    }

    private static async Task Shot(IPage page, string name, int width)
    {
        Directory.CreateDirectory(ScreenshotDir);
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(ScreenshotDir, $"{name}-{width}.png"), FullPage = true });
    }
}
