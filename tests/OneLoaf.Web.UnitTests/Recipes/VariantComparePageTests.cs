using OneLoaf.Web.Features.Recipes.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class VariantComparePageTests : BunitContext
{
    private static readonly Guid VariantId = Guid.NewGuid();
    private static readonly Guid ParentId = Guid.NewGuid();
    private readonly CompareApi _api = new();

    public VariantComparePageTests() => this.AddRecipesFakes(_api);

    [Fact]
    public void Render_Variant_ShowsOnlyChangesUntilAllLinesIsChosen()
    {
        var cut = Render<VariantComparePage>(p => p.Add(page => page.Id, VariantId));

        cut.WaitForAssertion(() => Assert.Contains("Variant of Country sourdough", cut.Find(".compare__head").TextContent));
        var rows = cut.FindAll("#compare-ingredients ~ .ol-table-wrap tbody tr");
        Assert.Single(rows);
        Assert.Contains("390 g", rows[0].TextContent);
        Assert.Contains("Changed", rows[0].TextContent);
        Assert.Contains("1 change", cut.Find(".compare__head").TextContent);

        cut.FindAll(".ol-segmented__opt")[1].Click();

        Assert.Equal(2, cut.FindAll("#compare-ingredients ~ .ol-table-wrap tbody tr").Count);
        Assert.Contains("Same", cut.Find("#compare-ingredients ~ .ol-table-wrap tbody").TextContent);
    }

    [Fact]
    public void Render_NotAVariant_ExplainsWithTheApiReason()
    {
        _api.NotVariant = true;

        var cut = Render<VariantComparePage>(p => p.Add(page => page.Id, VariantId));

        cut.WaitForAssertion(() => Assert.Contains("Only a variant can be compared", cut.Find(".ol-empty").TextContent));
    }

    private static RecipeIngredientDto Line(string name, decimal quantity) =>
        new(Guid.NewGuid(), Guid.NewGuid(), name, "flour", "g", quantity, false, 1, null, null, false);

    private sealed class CompareApi : RecipesApiStub
    {
        public bool NotVariant { get; set; }

        public override Task<ApiResult<RecipeComparisonDto>> CompareAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(NotVariant
                ? ApiResult<RecipeComparisonDto>.Fail(ApiError.FromProblem(new ProblemDetailsDto(
                    "https://oneloaf.app/problems/business-rule", "Rule", 422, "Only a variant can be compared with its parent recipe.", null, null, null, null), 422, null, null))
                : ApiResult<RecipeComparisonDto>.Ok(new RecipeComparisonDto(id, ParentId, [],
                    [
                        new IngredientComparisonDto("unchanged", Line("Bread flour", 500m), Line("Bread flour", 500m)),
                        new IngredientComparisonDto("changed", Line("Water", 375m), Line("Water", 390m)),
                    ],
                    [])));

        public override Task<ApiResult<RecipeDto>> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<RecipeDto>.Ok(new RecipeDto(id, id == ParentId ? null : ParentId,
                id == ParentId ? "Country sourdough" : "Seeded country", null, 1, null, null, false, [], [],
                DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)));
    }
}
