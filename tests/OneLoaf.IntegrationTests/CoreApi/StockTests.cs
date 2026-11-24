using System.Net;
using System.Net.Http.Json;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.Application.Starters;
using OneLoaf.CoreApi.Application.Warnings;
using OneLoaf.Domain;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

/// <summary>Stock of ingredients end to end: purchases, batches, starter feedings, corrections and prices.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class StockTests(ApiFixture fixture)
{
    private const string Ingredients = "/api/v1/ingredients";
    private const string Batches = "/api/v1/batches";

    private static readonly DateTimeOffset Start = new(2030, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Stock_PurchaseBatchFeedingAndCorrection_KeepTheLedger()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        var flour = await (await anna.PostAsJsonAsync(Ingredients, new IngredientRequest("Stock test flour", "flour", "g", null),
            CoreApiFactory.Json, Ct)).ReadAsync<IngredientDto>();
        Assert.False(flour.TrackStock);
        Assert.Null(flour.Stock);
        async Task<IngredientDto> Flour() => await (await anna.GetAsync($"{Ingredients}/{flour.Id}", Ct)).ReadAsync<IngredientDto>();

        // A purchase of two 1 kg bags adds 2000 g, switches the stock on and sets the price of that date.
        using var purchase = await anna.PostAsJsonAsync($"{Ingredients}/{flour.Id}/records",
            new IngredientRecordRequest(2, 1000, 3m, "EUR", new DateOnly(2026, 11, 20)), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.OK, purchase.StatusCode);
        var bought = await Flour();
        Assert.True(bought.TrackStock);
        Assert.Equal(2000m, bought.Stock);
        Assert.Equal(0.0015m, bought.CurrentPrice!.UnitPrice);

        var recipe = await (await anna.PostAsJsonAsync("/api/v1/recipes", new RecipeRequest("Stock test loaf", null, 1, null, null,
            [new(null, flour.Id, 500)], [new(null, "Bake", "bake", 40, 45)]), CoreApiFactory.Json, Ct)).ReadAsync<RecipeDto>();
        BatchRequest Batch(int loaves, decimal? scale = null) =>
            new("Stock test", Start, [new(recipe.Id, loaves)], new DateOnly(2026, 11, 20), IngredientScale: scale);

        // Six loaves need 3000 g: 1000 g are missing; the stock is enough for four loaves or two thirds of the dough.
        var preview = await (await anna.PostAsJsonAsync($"{Batches}/preview", Batch(6), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
        var shortage = Assert.Single(preview.Stock!.Shortages);
        Assert.Equal(1000m, shortage.Missing);
        Assert.Equal(4, preview.Stock.MaxLoaves);
        Assert.Equal(0.6666m, preview.Stock.MaxScale);
        Assert.Equal(2000m, (await Flour()).Stock);

        // Creating the batch takes the flour; re-planning changes the stock by the difference.
        var batch = await (await anna.PostAsJsonAsync(Batches, Batch(2), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
        Assert.Empty(batch.Stock!.Shortages);
        Assert.Equal(1000m, (await Flour()).Stock);
        await anna.PutAsJsonAsync($"{Batches}/{batch.Id}", Batch(3), CoreApiFactory.Json, Ct);
        Assert.Equal(500m, (await Flour()).Stock);

        // Cancelling an unused batch returns the flour; deleting it keeps the ledger.
        using var cancelled = await anna.PostAsync($"{Batches}/{batch.Id}/cancel?ingredientsUsed=false", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, cancelled.StatusCode);
        Assert.Equal(2000m, (await Flour()).Stock);
        using var deleted = await anna.DeleteAsync($"{Batches}/{batch.Id}?ingredientsUsed=false", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(2000m, (await Flour()).Stock);
        var ledger = await (await anna.GetAsync($"{Ingredients}/{flour.Id}/stock-movements", Ct)).ReadAsync<PagedResult<StockMovementDto>>();
        Assert.Contains(ledger.Items, m => m.Type == StockMovementType.BatchReturn && m.Quantity == 1500m && m.BatchId is null);

        // Baking four loaves from half of the dough takes 1000 g instead of 2000 g and halves the ingredient cost.
        var scaled = await (await anna.PostAsJsonAsync(Batches, Batch(4, 0.5m), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
        Assert.Equal(0.5m, scaled.IngredientScale);
        Assert.Equal(1000m, (await Flour()).Stock);

        // A starter is an ingredient of its own. A feeding takes the flour of the linked ingredient; two thirds of the
        // fed starter become stock when it is ripe, and a batch planned after the peak can count on it.
        var starter = await (await anna.PostAsJsonAsync("/api/v1/starters",
            new StarterRequest("Stock test starter", 100, FeedingMode.Regular, 24, RiseHours: 6, FlourIngredientId: flour.Id),
            CoreApiFactory.Json, Ct)).ReadAsync<StarterDto>();
        var asIngredient = await (await anna.GetAsync($"{Ingredients}/{starter.IngredientId}", Ct)).ReadAsync<IngredientDto>();
        Assert.Equal("Stock test starter", asIngredient.Name);
        Assert.Equal("starter", asIngredient.CategoryCode);
        Assert.Equal(starter.Id, asIngredient.StarterId);

        var fedAt = DateTimeOffset.UtcNow.AddHours(-1);
        await anna.PostAsJsonAsync($"/api/v1/starters/{starter.Id}/feedings", new FeedingRequest(fedAt, 50, 50, 20), CoreApiFactory.Json, Ct);
        Assert.Equal(950m, (await Flour()).Stock);
        var ripening = await (await anna.GetAsync($"/api/v1/starters/{starter.Id}", Ct)).ReadAsync<StarterDto>();
        Assert.Equal(120m, ripening.AmountG);
        Assert.Equal(0m, ripening.UsableG);
        Assert.Equal(120m, ripening.SuggestedKeptG);

        var levain = await (await anna.PostAsJsonAsync("/api/v1/recipes", new RecipeRequest("Stock test levain loaf", null, 1, null, null,
            [new(null, starter.IngredientId!.Value, 50)], [new(null, "Bake", "bake", 40, 45)]), CoreApiFactory.Json, Ct)).ReadAsync<RecipeDto>();
        BatchRequest Levain(DateTimeOffset start) => new("Levain test", start, [new(levain.Id, 1)], new DateOnly(2026, 11, 20));
        var tooEarly = await (await anna.PostAsJsonAsync($"{Batches}/preview", Levain(fedAt.AddHours(2)), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
        Assert.Equal(50m, Assert.Single(tooEarly.Stock!.Shortages).Missing);
        var afterPeak = await (await anna.PostAsJsonAsync($"{Batches}/preview", Levain(fedAt.AddHours(7)), CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
        Assert.Empty(afterPeak.Stock!.Shortages);

        // Renaming the starter renames its ingredient: one name for both.
        await anna.PutAsJsonAsync($"/api/v1/starters/{starter.Id}",
            new StarterRequest("Stock test levain", 100, FeedingMode.Regular, 24, RiseHours: 6, FlourIngredientId: flour.Id, UsableShare: 0.5m),
            CoreApiFactory.Json, Ct);
        Assert.Equal("Stock test levain",
            (await (await anna.GetAsync($"{Ingredients}/{starter.IngredientId}", Ct)).ReadAsync<IngredientDto>()).Name);

        // A correction sets the stock to the counted quantity; below the reorder level the ingredient is low.
        using var adjusted = await anna.PostAsJsonAsync($"{Ingredients}/{flour.Id}/stock-adjustments",
            new StockAdjustmentRequest(900m, "Counted the shelf"), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Created, adjusted.StatusCode);
        var low = await (await anna.PutAsJsonAsync($"{Ingredients}/{flour.Id}",
            new IngredientRequest("Stock test flour", "flour", "g", null, ReorderLevel: 1000m), CoreApiFactory.Json, Ct)).ReadAsync<IngredientDto>();
        Assert.Equal(900m, low.Stock);
        Assert.True(low.IsLowStock);
        var warnings = await (await anna.GetAsync("/api/v1/warnings", Ct)).ReadAsync<IReadOnlyList<WarningDto>>();
        Assert.Contains(warnings, w => w.IssueCode == "low_stock" && w.EntityId == flour.Id);

        // Switching the stock off hides it and stops the warning.
        var off = await (await anna.PutAsJsonAsync($"{Ingredients}/{flour.Id}",
            new IngredientRequest("Stock test flour", "flour", "g", null, TrackStock: false), CoreApiFactory.Json, Ct)).ReadAsync<IngredientDto>();
        Assert.Null(off.Stock);
        Assert.False(off.IsLowStock);
    }

    [Fact]
    public async Task US12_PriceEnteredByMistake_CanBeReplacedAndDeleted()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        var salt = await (await anna.PostAsJsonAsync(Ingredients, new IngredientRequest("Price test salt", "salt", "g", null),
            CoreApiFactory.Json, Ct)).ReadAsync<IngredientDto>();
        var prices = $"{Ingredients}/{salt.Id}/prices";
        var wrong = await (await anna.PostAsJsonAsync(prices, new PriceRequest(90m, 1000, "EUR", new DateOnly(2026, 11, 1)),
            CoreApiFactory.Json, Ct)).ReadAsync<PriceDto>();

        var fixedPrice = await (await anna.PutAsJsonAsync($"{prices}/{wrong.Id}",
            new PriceRequest(0.9m, 1000, "EUR", new DateOnly(2026, 11, 2)), CoreApiFactory.Json, Ct)).ReadAsync<PriceDto>();
        Assert.Equal(0.0009m, fixedPrice.UnitPrice);
        Assert.Equal(new DateOnly(2026, 11, 2), fixedPrice.EffectiveFrom);

        using var deleted = await anna.DeleteAsync($"{prices}/{wrong.Id}", Ct);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await (await anna.GetAsync(prices, Ct)).ReadAsync<IReadOnlyList<PriceDto>>());
        using var again = await anna.DeleteAsync($"{prices}/{wrong.Id}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task US34_RecipeDraft_CreatesMissingIngredientsWithoutPrices()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        using var response = await anna.PostAsJsonAsync("/api/v1/recipes/drafts", new RecipeDraftRequest(
            "Draft test loaf", "A first loaf", 1, 900, null,
            [new("Draft test flour", "flour", "g", 500), new("Draft test water", "water", "g", 350)],
            [new(null, "Mix", "mix", 10, 15), new(null, "Bake", "bake", 40, 45)]), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.ReadAsync<RecipeDraftResponse>();
        Assert.Equal(["Draft test flour", "Draft test water"], draft.CreatedIngredients);
        Assert.Equal(2, draft.Recipe.Ingredients.Count);

        // A rejected draft leaves no ingredients behind: the unknown stage type fails after the ingredients were written.
        using var rejected = await anna.PostAsJsonAsync("/api/v1/recipes/drafts", new RecipeDraftRequest(
            "Rejected draft", null, 1, null, null, [new("Draft orphan", "flour", "g", 500)], [new(null, "Rest", "nap", 10, 15)]),
            CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        var found = await (await anna.GetAsync($"{Ingredients}?search=Draft%20orphan", Ct)).ReadAsync<PagedResult<IngredientDto>>();
        Assert.Empty(found.Items);
    }
}
