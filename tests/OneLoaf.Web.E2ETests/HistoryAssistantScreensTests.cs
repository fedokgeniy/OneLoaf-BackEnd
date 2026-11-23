using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>History, bake record and the assistant plan journey on recorded API answers at phone and desktop width (US-31–38).</summary>
public sealed class HistoryAssistantScreensTests(WebAppFixture app) : IClassFixture<WebAppFixture>
{
    private static readonly string ScreenshotDir = Path.Combine(AppContext.BaseDirectory, "TestResults", "screenshots");

    [Theory]
    [InlineData(360, 640)]
    [InlineData(1440, 900)]
    public async Task HistoryAndAssistant_RecordedData_PlanIsSavedOnlyOnConfirm(int width, int height)
    {
        Assert.SkipWhen(WebAppFixture.WebRoot is null, $"Set {WebAppFixture.WebRootVariable} to the published wwwroot.");
        await using var context = await app.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = app.BaseAddress.ToString(),
            ViewportSize = new ViewportSize { Width = width, Height = height },
        });
        var page = await context.NewPageAsync();
        var (recordStatus, record) = RecordedApi.Case("core-api", "GetBakeRecord", "typical");
        var recordId = (string)record!["id"]!;
        var confirms = 0;

        // Playwright tries the most recently registered route first: generic patterns before specific ones.
        await Route(page, "**/api/v1/auth/login", "core-api", "Login", "typical", login: true);
        await Route(page, "**/api/v1/batches?*", "core-api", "ListBatches", "calendar range");
        await Route(page, "**/api/v1/starters?*", "core-api", "ListStarters", "typical");
        await Route(page, "**/api/v1/reminders?*", "notification", "ListReminders", "upcoming");
        await Route(page, "**/api/v1/bake-records?*", "core-api", "ListBakeRecords", "typical");
        await Route(page, "**/api/v1/bake-records/statistics", "core-api", "GetBakeStatistics", "typical");
        await Route(page, $"**/api/v1/bake-records/{recordId}/stage-log", "core-api", "GetStageLog", "typical");
        await RecordedApi.RouteAsync(page, $"**/api/v1/bake-records/{recordId}", recordStatus, record);
        await Route(page, "**/api/v1/conversations", "assistant", "ListConversations", "typical");
        await Route(page, "**/api/v1/usage/me", "assistant", "GetMyUsage", "typical");
        await page.RouteAsync("**/api/v1/conversations", async route =>
        {
            var (status, body) = RecordedApi.Case("assistant", route.Request.Method == "POST" ? "CreateConversation" : "ListConversations", "typical");
            await route.FulfillAsync(new RouteFulfillOptions { Status = status, ContentType = "application/json", Body = body?.ToJsonString() });
        });
        await Route(page, "**/api/v1/conversations/*/messages", "assistant", "SendMessage", "plans a batch with tools (proposal to confirm)");
        await page.RouteAsync("**/api/v1/conversations/*/actions/*/confirm", async route =>
        {
            confirms++;
            var (status, body) = RecordedApi.Case("assistant", "ConfirmAction", "typical");
            await route.FulfillAsync(new RouteFulfillOptions { Status = status, ContentType = "application/json", Body = body?.ToJsonString() });
        });

        await page.GotoAsync("/login");
        await page.GetByLabel("E-mail").FillAsync("docs.baker.f71fcafd8@example.com");
        await page.GetByLabel("Password").FillAsync("Rye-Bread-2026!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Recent bakes" })).ToBeVisibleAsync();

        await page.GotoAsync("/history");
        await Assertions.Expect(page.Locator(".history__card").First).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "By recipe" })).ToBeVisibleAsync();
        await Shot(page, "history", width);

        await page.GotoAsync($"/history/{recordId}");
        await Assertions.Expect(page.GetByRole(AriaRole.Group, new() { Name = "Rating" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Stages" })).ToBeVisibleAsync();
        await Shot(page, "bake-record", width);

        await page.GotoAsync("/assistant");
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Message" }).FillAsync("Plan 2 loaves of my Country sourdough for next Saturday morning.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Send message" }).ClickAsync();
        var confirm = page.GetByRole(AriaRole.Button, new() { Name = "Confirm plan" });
        await Assertions.Expect(confirm).ToBeVisibleAsync();
        Assert.Equal(0, confirms);
        await Shot(page, "assistant", width);

        await confirm.ClickAsync();
        await Assertions.Expect(page.Locator(".ol-plan").GetByText("Saved as a batch.")).ToBeVisibleAsync();
        Assert.Equal(1, confirms);
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
