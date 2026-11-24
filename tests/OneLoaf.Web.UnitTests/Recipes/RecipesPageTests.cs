using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Web.Features.Recipes.Pages;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class RecipesPageTests : BunitContext
{
    private readonly FakeRecipesApi _api = new();

    public RecipesPageTests()
    {
        this.AddRecipesFakes(_api);
        Services.GetRequiredService<NavigationManager>().NavigateTo("recipes");
    }

    [Fact]
    public void Render_RecipesLoaded_ShowsCardsWithVariantParentAndArchivedBadge()
    {
        var parent = new RecipeSummaryDto(Guid.NewGuid(), "White homemade loaf", null, 1, false, DateTimeOffset.UnixEpoch);
        var variant = new RecipeSummaryDto(Guid.NewGuid(), "Seeded white", parent.Id, 2, true, DateTimeOffset.UnixEpoch);
        _api.Result = Page(parent, variant);

        var cut = Render<RecipesPage>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ol-card").Count));
        var seeded = cut.FindAll(".ol-card")[1];
        Assert.Equal($"recipes/{variant.Id}", seeded.GetAttribute("href"));
        Assert.Equal("Variant of White homemade loaf", seeded.QuerySelector(".ol-card__overline")!.TextContent);
        Assert.Contains("2 loaves", seeded.TextContent);
        Assert.Equal("Archived", seeded.QuerySelector(".ol-badge")!.TextContent);
        Assert.Equal("2 recipes", cut.Find("[role=status]").TextContent);
    }

    [Fact]
    public void ToggleArchived_SendsFilterAndUpdatesAddress()
    {
        _api.Result = Page();
        var cut = Render<RecipesPage>();
        cut.WaitForAssertion(() => cut.Find(".ol-empty"));
        Assert.Contains("No recipes yet", cut.Find(".ol-empty").TextContent);

        cut.FindAll(".ol-chip")[1].Click();

        cut.WaitForAssertion(() => Assert.True(_api.LastQuery!.IncludeArchived));
        Assert.EndsWith("recipes?archived=true", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
        cut.WaitForAssertion(() => Assert.Contains("No recipes match", cut.Find(".ol-empty").TextContent));
    }

    [Fact]
    public void Render_LoadFails_ShowsAlertAndRetries()
    {
        _api.Result = ApiResult<PagedResult<RecipeSummaryDto>>.Fail(ApiError.FromStatus(503, "corr-1"));
        var cut = Render<RecipesPage>();
        cut.WaitForAssertion(() => Assert.Contains("corr-1", cut.Find(".ol-alert").TextContent));

        _api.Result = Page(new RecipeSummaryDto(Guid.NewGuid(), "Rye", null, 1, false, DateTimeOffset.UnixEpoch));
        cut.Find(".ol-alert button").Click();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".ol-card")));
    }

    private static ApiResult<PagedResult<RecipeSummaryDto>> Page(params RecipeSummaryDto[] items) =>
        ApiResult<PagedResult<RecipeSummaryDto>>.Ok(new PagedResult<RecipeSummaryDto>(items, 1, 20, items.Length));

    private sealed class FakeRecipesApi : RecipesApiStub
    {
        public ApiResult<PagedResult<RecipeSummaryDto>> Result { get; set; } = Page();

        public RecipeQuery? LastQuery { get; private set; }

        public override Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListAsync(RecipeQuery query, int page, CancellationToken ct = default)
        {
            LastQuery = query;
            return Task.FromResult(Result);
        }




    }
}
