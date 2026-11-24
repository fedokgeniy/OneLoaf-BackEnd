using OneLoaf.CoreApi.Application.Batches;

namespace OneLoaf.CoreApi.UnitTests.Production;

public sealed class BatchStockPlannerTests
{
    private static readonly Guid Flour = Guid.NewGuid();
    private static readonly Guid Salt = Guid.NewGuid();

    [Fact]
    public void Evaluate_NoStockKeptIngredients_ReturnsNull() =>
        Assert.Null(BatchStockPlanner.Evaluate([], 2));

    [Fact]
    public void Evaluate_StockCoversTheBatch_HasNoShortagesAndNoOffers()
    {
        var stock = BatchStockPlanner.Evaluate([new(Flour, "Flour", "g", 1000, 1000, 5000)], 2);

        Assert.NotNull(stock);
        Assert.Empty(stock.Shortages);
        Assert.Null(stock.MaxLoaves);
        Assert.Null(stock.MaxScale);
    }

    [Fact]
    public void Evaluate_ScarcestIngredientDecidesLoavesAndScale()
    {
        // 4 loaves need 2000 g of flour and 40 g of salt; the stock has 1300 g and 100 g.
        var stock = BatchStockPlanner.Evaluate(
            [new(Flour, "Flour", "g", 2000, 2000, 1300), new(Salt, "Salt", "g", 40, 40, 100)], 4);

        Assert.NotNull(stock);
        var shortage = Assert.Single(stock.Shortages);
        Assert.Equal(Flour, shortage.IngredientId);
        Assert.Equal(700m, shortage.Missing);
        Assert.Equal(2, stock.MaxLoaves);
        Assert.Equal(0.65m, stock.MaxScale);
    }

    [Fact]
    public void Evaluate_EmptyOrNegativeStock_OffersNothing()
    {
        var stock = BatchStockPlanner.Evaluate([new(Flour, "Flour", "g", 500, 500, -20)], 1);

        Assert.NotNull(stock);
        Assert.Equal(520m, Assert.Single(stock.Shortages).Missing);
        Assert.Null(stock.MaxLoaves);
        Assert.Null(stock.MaxScale);
    }

    [Fact]
    public void Evaluate_SeveralItems_OffersOnlyTheScale()
    {
        var stock = BatchStockPlanner.Evaluate([new(Flour, "Flour", "g", 1000, 1000, 500)], null);

        Assert.NotNull(stock);
        Assert.Null(stock.MaxLoaves);
        Assert.Equal(0.5m, stock.MaxScale);
    }
}
