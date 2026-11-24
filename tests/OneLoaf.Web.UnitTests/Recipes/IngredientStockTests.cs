using OneLoaf.Web.Features.Recipes.Pages;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

/// <summary>Stock, purchases, price corrections and flour details on <c>/ingredients</c>.</summary>
public sealed class IngredientStockTests : BunitContext
{
    private static readonly Guid FlourId = Guid.NewGuid();

    private static readonly IngredientDto Flour = new(Guid.NewGuid(), "Bread flour", "flour", "g", null, null, null,
        new CurrentPriceDto(1.35m, 1000m, 0.00135m, "EUR", new DateOnly(2026, 11, 1)), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
        TrackStock: true, Stock: 400m, ReorderLevel: 1000m, IsLowStock: true);

    private static readonly IngredientDto Salt = new(Guid.NewGuid(), "Salt", "water", "g", null, null, null, null,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private readonly Catalog _catalog = new();

    public IngredientStockTests() => this.AddRecipesFakes(new RecipesApiStub(), _catalog);

    [Fact]
    public void List_StockKeptIngredient_ShowsTheStockAndTheLowBadgeAsText()
    {
        var cut = Render<IngredientsPage>();

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ingredients__row").Count));
        var rows = cut.FindAll(".ingredients__row");
        Assert.Contains("400 g in stock", rows[0].TextContent, StringComparison.Ordinal);
        Assert.Contains("Low stock", rows[0].TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("in stock", rows[1].TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void AddRecord_PacksAndSize_SendsThemAndShowsTheLedger()
    {
        var cut = Render<IngredientsPage>();
        cut.WaitForAssertion(() => cut.FindAll(".ingredients__row")[0].Click());
        cut.WaitForAssertion(() => Assert.Contains("Saturday bake", cut.Find("#ingredient-panel-stock").TextContent, StringComparison.Ordinal));
        Assert.Contains("Purchase", cut.Find("#ingredient-panel-stock").TextContent, StringComparison.Ordinal);
        Assert.Contains("5 × ", cut.Find("#ingredient-panel-stock").TextContent, StringComparison.Ordinal);

        cut.FindAll(".ingredients__card button").First(b => b.TextContent.Contains("Add record", StringComparison.Ordinal)).Click();
        cut.FindAll("#record-form input[inputmode]")[0].Change("3");
        cut.FindAll("#record-form input[inputmode]")[1].Change("2500");
        cut.FindAll("#record-form input[inputmode]")[2].Change("9.60");
        cut.Find("#record-form").Submit();

        cut.WaitForAssertion(() => Assert.NotNull(_catalog.LastRecord));
        Assert.Equal(3, _catalog.LastRecord!.PackCount);
        Assert.Equal(2500m, _catalog.LastRecord.PackSize);
        Assert.Equal(9.60m, _catalog.LastRecord.TotalCost);
        Assert.False(_catalog.LastRecord.PriceOnly);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#record-form")));
    }

    [Fact]
    public void AddRecord_OnlyPrice_SendsThePriceWithoutTouchingTheStock()
    {
        var cut = Render<IngredientsPage>();
        cut.WaitForAssertion(() => cut.FindAll(".ingredients__row")[1].Click());
        cut.WaitForAssertion(() => cut.Find(".ingredients__card"));

        cut.FindAll(".ingredients__card button").First(b => b.TextContent.Contains("Add record", StringComparison.Ordinal)).Click();
        cut.FindAll("#record-form input[inputmode]")[1].Change("1000");
        cut.FindAll("#record-form input[inputmode]")[2].Change("0.80");
        cut.Find("#record-form input[role=switch]").Change(true);
        Assert.Contains("stays as it is", cut.Find("#record-form").TextContent, StringComparison.Ordinal);
        cut.Find("#record-form").Submit();

        cut.WaitForAssertion(() => Assert.True(_catalog.LastRecord?.PriceOnly));
        Assert.Equal(0.80m, _catalog.LastRecord!.TotalCost);
    }

    [Fact]
    public void DeletePrice_Confirmed_CallsTheApiForThatPrice()
    {
        var cut = Render<IngredientsPage>();
        cut.WaitForAssertion(() => cut.FindAll(".ingredients__row")[0].Click());
        cut.WaitForAssertion(() => cut.Find("#ingredient-panel-prices table"));

        cut.Find("#ingredient-panel-prices button[aria-label^='Delete the price']").Click();
        cut.FindAll("button").Single(b => b.TextContent.Contains("Delete price", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Equal(Catalog.PriceId, _catalog.DeletedPrice));
    }

    [Fact]
    public void AddFlour_FromTheIngredientForm_SelectsTheNewFlour()
    {
        var cut = Render<IngredientsPage>();
        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll(".ingredients__row").Count));
        cut.FindAll("button").First(b => b.TextContent.Contains("New ingredient", StringComparison.Ordinal)).Click();
        cut.Find("#ingredient-form select").Change("flour");

        cut.FindAll("#ingredient-form button").Single(b => b.TextContent.Contains("Add flour", StringComparison.Ordinal)).Click();
        cut.Find("#flour-form input").Change("Rye T1150");
        cut.Find("#flour-form select").Change("wheat");
        cut.Find("#flour-form").Submit();

        cut.WaitForAssertion(() => Assert.Equal("Rye T1150", _catalog.LastFlour?.Name));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("#flour-form")));
        var flourSelect = cut.FindAll("#ingredient-form select")[2];
        Assert.Equal(FlourId.ToString(), flourSelect.GetAttribute("value"));
    }

    private sealed class Catalog : CatalogApiStub
    {
        public static readonly Guid PriceId = Guid.NewGuid();

        public IngredientRecordRequest? LastRecord { get; private set; }

        public Guid? DeletedPrice { get; private set; }

        public FlourRequest? LastFlour { get; private set; }

        public override Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(string? search, string? category, int page, int pageSize, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<PagedResult<IngredientDto>>.Ok(new PagedResult<IngredientDto>([Flour, Salt], 1, pageSize, 2)));

        public override Task<ApiResult<IReadOnlyList<PriceDto>>> GetPricesAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<IReadOnlyList<PriceDto>>.Ok([new PriceDto(PriceId, 1.35m, 1000m, 0.00135m, "EUR", new DateOnly(2026, 11, 1), null)]));

        public override Task<ApiResult<PagedResult<StockMovementDto>>> ListMovementsAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<PagedResult<StockMovementDto>>.Ok(new PagedResult<StockMovementDto>(
            [
                new StockMovementDto(Guid.NewGuid(), "purchase", 5000m, new DateOnly(2026, 11, 20), null, null, 5, 1000m, 6m, "EUR", null, DateTimeOffset.UnixEpoch),
                new StockMovementDto(Guid.NewGuid(), "batch_consumption", -4600m, new DateOnly(2026, 11, 22), Guid.NewGuid(), "Saturday bake", null, null, null, null, null, DateTimeOffset.UnixEpoch),
            ], 1, 100, 2)));

        public override Task<ApiResult<IngredientDto>> AddRecordAsync(Guid id, IngredientRecordRequest request, CancellationToken ct = default)
        {
            LastRecord = request;
            return Task.FromResult(ApiResult<IngredientDto>.Ok(Flour));
        }

        public override Task<ApiResult> DeletePriceAsync(Guid id, Guid priceId, CancellationToken ct = default)
        {
            DeletedPrice = priceId;
            return Task.FromResult(ApiResult.Ok);
        }

        public override Task<ApiResult<FlourDto>> CreateFlourAsync(FlourRequest request, CancellationToken ct = default)
        {
            LastFlour = request;
            return Task.FromResult(ApiResult<FlourDto>.Ok(new FlourDto(FlourId, request.Name, request.FlourTypeCode, request.ProteinPercent,
                request.Grind, request.Notes, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch)));
        }
    }
}
