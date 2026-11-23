using Microsoft.AspNetCore.Components.Web;
using OneLoaf.Web.Features.Recipes.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class RecipeDetailPageTests : BunitContext
{
    private static readonly Guid Id = Guid.NewGuid();
    private readonly DetailApi _api = new();

    public RecipeDetailPageTests()
    {
        this.AddRecipesFakes(_api);
    }

    [Fact]
    public void Render_Recipe_ShowsApiFiguresAndAllSections()
    {
        var cut = Render<RecipeDetailPage>(p => p.Add(page => page.Id, Id));

        cut.WaitForAssertion(() => Assert.Contains("6.05 EUR", cut.Find(".ol-stats").TextContent));
        var stats = cut.Find(".ol-stats").TextContent;
        Assert.Contains("2 880 g", stats);
        Assert.Contains("1 cycle", stats);
        Assert.Contains("300 g", stats);
        Assert.Contains("Bread flour · 12.8 % protein", cut.Find(".recipe__badges").TextContent);
        Assert.Contains("Cost complete", cut.Find(".recipe__badges").TextContent);
        Assert.Contains("1 500 g", cut.Find("#recipe-panel-ingredients").TextContent);
        Assert.Contains("4–5 h", cut.Find("#recipe-panel-stages").TextContent);
        Assert.Contains("cold · 4 °C", cut.Find("#recipe-panel-stages").TextContent);
        Assert.Contains("Ready", cut.Find("#recipe-panel-timeline").TextContent);
        Assert.Equal("true", cut.Find("#recipe-tab-cost").GetAttribute("aria-selected"));
    }

    [Fact]
    public void Keyboard_ArrowRightOnTabs_SelectsTimelineAndHidesCostOnSmallScreens()
    {
        var cut = Render<RecipeDetailPage>(p => p.Add(page => page.Id, Id));
        cut.WaitForAssertion(() => cut.Find("#recipe-tab-cost"));

        cut.Find("#recipe-tab-cost").KeyDown(new KeyboardEventArgs { Key = "ArrowRight" });

        Assert.Equal("true", cut.Find("#recipe-tab-timeline").GetAttribute("aria-selected"));
        Assert.Equal("0", cut.Find("#recipe-tab-timeline").GetAttribute("tabindex"));
        Assert.Contains("recipe__panel--inactive", cut.Find("#recipe-panel-cost").ClassList);
    }

    [Fact]
    public void Stepper_MoreLoaves_RequestsTheApiAgain()
    {
        var cut = Render<RecipeDetailPage>(p => p.Add(page => page.Id, Id));
        cut.WaitForAssertion(() => Assert.Single(_api.CostRequests));

        cut.Find("button[aria-label='More loaves']").Click();

        cut.WaitForAssertion(() => Assert.Equal(2, _api.CostRequests.Last()), TimeSpan.FromSeconds(2));
        Assert.Equal("2", cut.Find("input[type=number]").GetAttribute("value"));
    }

    [Fact]
    public void Render_RecipeNotFound_ShowsNotFoundState()
    {
        _api.Missing = true;

        var cut = Render<RecipeDetailPage>(p => p.Add(page => page.Id, Id));

        cut.WaitForAssertion(() => Assert.Contains("Recipe not found", cut.Find(".ol-empty").TextContent));
    }

    private sealed class DetailApi : RecipesApiStub
    {
        public bool Missing { get; set; }

        public List<int> CostRequests { get; } = [];

        public override Task<ApiResult<RecipeDto>> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Missing
                ? ApiResult<RecipeDto>.Fail(ApiError.FromStatus(404))
                : ApiResult<RecipeDto>.Ok(new RecipeDto(id, null, "Country sourdough", "Open crumb.", 1, 900m, null, false,
                    [new RecipeIngredientDto(Guid.NewGuid(), Guid.NewGuid(), "Bread flour", "flour", "g", 500m, false, 1, null, 12.8m, false)],
                    [
                        new RecipeStageDto(Guid.NewGuid(), 1, "Bulk fermentation", "bulk_fermentation", 240, 300, 0, null, 24m, false, false, false, null, false),
                        new RecipeStageDto(Guid.NewGuid(), 2, "Cold proof", "proof", 600, 720, 0, "cold", 4m, false, false, false, null, false),
                    ],
                    DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)));

        public override Task<ApiResult<ScaledRecipeDto>> ScaleAsync(Guid id, int loaves, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<ScaledRecipeDto>.Ok(new ScaledRecipeDto(id, 1, loaves,
                [new ScaledIngredientDto(Guid.NewGuid(), Guid.NewGuid(), "Bread flour", "g", 1500m, false)])));

        public override Task<ApiResult<CostBreakdownDto>> GetCostAsync(Guid id, int loaves, DateOnly? asOf, CancellationToken ct = default)
        {
            CostRequests.Add(loaves);
            return Task.FromResult(ApiResult<CostBreakdownDto>.Ok(new CostBreakdownDto(id, loaves, new DateOnly(2026, 11, 23), "EUR", true,
                [new CostLineDto(1, "total", null, "Total", loaves, "loaves", null, 6.05m, true)], 6.05m, [])));
        }

        public override Task<ApiResult<ProductionFiguresDto>> GetProductionFiguresAsync(Guid id, int loaves, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<ProductionFiguresDto>.Ok(new ProductionFiguresDto(id, loaves, 2880m, 1, 300m)));

        public override Task<ApiResult<TimelineDto>> GetTimelineAsync(Guid id, DateTimeOffset start, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<TimelineDto>.Ok(new TimelineDto(id, start,
                [new TimelineStageDto(Guid.NewGuid(), 1, "Bulk fermentation", "bulk_fermentation", 240, 300, start, start.AddHours(4), start.AddHours(5))],
                start.AddHours(15), start.AddHours(18))));
    }
}
