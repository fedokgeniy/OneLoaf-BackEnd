using OneLoaf.Web.Features.Recipes.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class IngredientsPageTests : BunitContext
{
    private static readonly IngredientDto Flour = new(Guid.NewGuid(), "Bread flour", "flour", "g", null, "Bread flour T550", 12.8m,
        new CurrentPriceDto(1.35m, 1000m, 0.00135m, "EUR", new DateOnly(2026, 11, 1)), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static readonly IngredientDto Seeds = new(Guid.NewGuid(), "Sunflower seeds", "flour", "g", null, null, null, null,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private readonly Catalog _catalog = new();

    public IngredientsPageTests() => this.AddRecipesFakes(new RecipesApiStub(), _catalog);

    [Fact]
    public void Select_Ingredient_ShowsPriceHistoryWithChange()
    {
        var cut = Render<IngredientsPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ingredients__row").Count));
        Assert.Contains("1.35 /kg", cut.FindAll(".ingredients__row")[0].TextContent);
        Assert.Contains("Price missing", cut.FindAll(".ingredients__row")[1].TextContent);

        cut.FindAll(".ingredients__row")[0].Click();

        cut.WaitForAssertion(() => Assert.Contains("+12.5 %", cut.Find(".ingredients__card table").TextContent));
        Assert.Contains("12.8 %", cut.Find(".ingredients__card .ol-stats").TextContent);
        Assert.Equal("true", cut.FindAll(".ingredients__row")[0].GetAttribute("aria-current"));
    }

    [Fact]
    public void AddPrice_DateAlreadyHasPrice_ShowsConflictUnderTheDate()
    {
        var cut = Render<IngredientsPage>();
        cut.WaitForAssertion(() => cut.FindAll(".ingredients__row")[0].Click());
        cut.WaitForAssertion(() => cut.Find(".form-grid"));

        cut.Find(".form-grid input[inputmode=decimal]").Change("1.40");
        cut.Find(".form-grid").Submit();

        cut.WaitForAssertion(() => Assert.Contains("A price for this date already exists.", cut.Find("input[type=date]").ParentElement!.ParentElement!.TextContent));
        Assert.Equal(1.40m, _catalog.LastPrice!.Price);
    }

    private sealed class Catalog : CatalogApiStub
    {
        public PriceRequest? LastPrice { get; private set; }

        public override Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(string? search, string? category, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<PagedResult<IngredientDto>>.Ok(new PagedResult<IngredientDto>([Flour, Seeds], 1, pageSize, 2)));

        public override Task<ApiResult<IReadOnlyList<PriceDto>>> GetPricesAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<IReadOnlyList<PriceDto>>.Ok(
            [
                new PriceDto(Guid.NewGuid(), 1.35m, 1000m, 0.00135m, "EUR", new DateOnly(2026, 11, 1), 12.5m),
                new PriceDto(Guid.NewGuid(), 1.20m, 1000m, 0.0012m, "EUR", new DateOnly(2026, 10, 1), null),
            ]));

        public override Task<ApiResult<PriceDto>> AddPriceAsync(Guid id, PriceRequest request, CancellationToken ct = default)
        {
            LastPrice = request;
            return Task.FromResult(ApiResult<PriceDto>.Fail(ApiError.FromProblem(new ProblemDetailsDto(
                "https://oneloaf.app/problems/conflict", "Conflict", 409, "A price for this date already exists.", null, null, null, null), 409, null, null)));
        }
    }
}
