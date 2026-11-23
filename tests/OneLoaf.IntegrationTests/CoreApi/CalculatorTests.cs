using System.Net;
using System.Net.Http.Json;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

/// <summary>Acceptance criteria of the calculator with the reference values of the business analysis.</summary>
[Collection(ApiTestGroup.Name)]
public sealed class CalculatorTests(ApiFixture fixture)
{
    private static string Recipe(Guid id) => $"/api/v1/recipes/{id}";

    [Fact]
    public async Task US14_CostFor3Loaves_TotalIs1122AndEqualsSumOfLines()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var cost = await (await anna.GetAsync($"{Recipe(Seed.R1)}/cost?loaves=3&asOf=2026-11-15", Ct)).ReadAsync<CostBreakdownDto>();

        Assert.Equal(11.22m, cost.Total);
        Assert.True(cost.IsComplete);
        Assert.Equal("EUR", cost.Currency);
        Assert.Equal(cost.Total, cost.Lines.Where(l => l.Type != CostLineTypes.Total).Sum(l => l.Amount));
        Assert.Equal(
            ["ingredient", "ingredient", "ingredient", "ingredient", "electricity", "packaging", "labor", "loss", "total"],
            cost.Lines.Select(l => l.Type));
        Assert.Equal(0.45m, Amount(cost, CostLineTypes.Packaging));
        Assert.Equal(1.02m, Amount(cost, CostLineTypes.Loss));
        Assert.Equal(7.00m, Amount(cost, CostLineTypes.Labor));
        Assert.Equal(0.52m, Amount(cost, CostLineTypes.Electricity));
        Assert.Empty(cost.MissingPrices);
    }

    [Fact]
    public async Task US15_CostAsOf15September_UsesTheOlderFlourPrice()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var cost = await (await anna.GetAsync($"{Recipe(Seed.R1)}/cost?loaves=3&asOf=2026-09-15", Ct)).ReadAsync<CostBreakdownDto>();

        Assert.Equal(1.80m, cost.Lines.Single(l => l.IngredientId == Seed.AnnaBreadFlourIngredient).Amount);
    }

    [Fact]
    public async Task UC01A4_MissingPrice_CostIsIncompleteWithTheMissingIngredient()
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);

        var cost = await (await boris.GetAsync($"{Recipe(Seed.R5)}/cost?loaves=1&asOf=2026-11-15", Ct)).ReadAsync<CostBreakdownDto>();

        Assert.False(cost.IsComplete);
        Assert.Single(cost.MissingPrices);
        Assert.Null(cost.Lines.Single(l => l.IngredientId == cost.MissingPrices[0].IngredientId).Amount);
    }

    [Fact]
    public async Task US19_US50_OvenLoadingWithinCapacityAndCsvExport()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        using var boris = fixture.CreateClientAs(Seed.Boris);
        var request = new OvenLoadingRequest([new OvenLoadingItem(Seed.R1, 6)]);

        var plan = await (await anna.PostAsJsonAsync("/api/v1/calculations/oven-loading", request, CoreApiFactory.Json, Ct)).ReadAsync<OvenLoadingPlanDto>();
        using var noCapacity = await boris.PostAsJsonAsync("/api/v1/calculations/oven-loading",
            new OvenLoadingRequest([new OvenLoadingItem(Seed.R5, 2)]), CoreApiFactory.Json, Ct);
        using var csv = await anna.GetAsync("/api/v1/export/recipes.csv", Ct);
        var text = await csv.Content.ReadAsStringAsync(Ct);

        Assert.Equal([4, 2], plan.Rounds.Select(r => r.Loaves));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noCapacity.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("title,is_variant,yield_loaves", text.TrimStart('﻿'), StringComparison.Ordinal);
        Assert.Contains("White homemade loaf", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cache_PriceAdded_NextCostIsRecalculated()
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);
        var url = $"{Recipe(Seed.R5)}/cost?loaves=1&asOf=2031-01-01";

        var before = await (await boris.GetAsync(url, Ct)).ReadAsync<CostBreakdownDto>();
        var cached = await (await boris.GetAsync(url, Ct)).ReadAsync<CostBreakdownDto>();
        using var price = await boris.PostAsJsonAsync($"/api/v1/ingredients/{before.MissingPrices[0].IngredientId}/prices",
            new PriceRequest(2.00m, 1000m, "EUR", new DateOnly(2030, 12, 1)), CoreApiFactory.Json, Ct);
        var after = await (await boris.GetAsync(url, Ct)).ReadAsync<CostBreakdownDto>();

        Assert.NotEmpty(cached.MissingPrices);
        Assert.Equal(HttpStatusCode.Created, price.StatusCode);
        Assert.Empty(after.MissingPrices);
    }

    [Fact]
    public async Task US16_TimelineFromNoon_ReadyBetween0200And0400()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var timeline = await (await anna.GetAsync($"{Recipe(Seed.R1)}/timeline?start=2026-11-20T12:00:00Z", Ct))
            .ReadAsync<TimelineDto>();

        Assert.Equal(5, timeline.Stages.Count);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 2, 0, 0, TimeSpan.Zero), timeline.ReadyFrom);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 4, 0, 0, TimeSpan.Zero), timeline.ReadyTo);
        Assert.Equal(timeline.ReadyTo, timeline.Stages.Max(s => s.PlannedEndMaxAt));
    }

    [Fact]
    public async Task Timeline_KeepsTheOffsetOfTheRequest()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var timeline = await (await anna.GetAsync($"{Recipe(Seed.R1)}/timeline?start=2026-11-20T14:00:00%2B02:00", Ct))
            .ReadAsync<TimelineDto>();

        Assert.Equal(TimeSpan.FromHours(2), timeline.ReadyFrom.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 21, 4, 0, 0, TimeSpan.FromHours(2)), timeline.ReadyFrom);
    }

    [Fact]
    public async Task US52_ReadyAt0800_StartBetween1600And1800PreviousDay()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var window = await (await anna.GetAsync($"{Recipe(Seed.R1)}/start-window?readyAt=2026-11-20T08:00:00Z", Ct))
            .ReadAsync<StartWindowDto>();

        Assert.Equal(new DateTimeOffset(2026, 11, 19, 16, 0, 0, TimeSpan.Zero), window.StartFrom);
        Assert.Equal(new DateTimeOffset(2026, 11, 19, 18, 0, 0, TimeSpan.Zero), window.StartTo);
    }

    [Fact]
    public async Task US17_US18_ProductionFiguresFor4Loaves()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var figures = await (await anna.GetAsync($"{Recipe(Seed.R1)}/production-figures?loaves=4", Ct))
            .ReadAsync<ProductionFiguresDto>();

        Assert.Equal(3840m, figures.DoughWeightG);
        Assert.Equal(1, figures.KneadingCycles);
        Assert.Equal(400m, figures.StarterAmountG);
    }

    [Fact]
    public async Task US17_NoBowlConfigured_KneadingCyclesIsNull()
    {
        using var boris = fixture.CreateClientAs(Seed.Boris);

        var figures = await (await boris.GetAsync($"{Recipe(Seed.R5)}/production-figures?loaves=1", Ct))
            .ReadAsync<ProductionFiguresDto>();

        Assert.Null(figures.KneadingCycles);
    }

    [Fact]
    public async Task US06_ScaledTo3Loaves_FlourIs1500Grams()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        var scaled = await (await anna.GetAsync($"{Recipe(Seed.R1)}/scaled?loaves=3", Ct)).ReadAsync<ScaledRecipeDto>();

        Assert.Equal(1500m, scaled.Ingredients.Single(i => i.IngredientId == Seed.AnnaBreadFlourIngredient).Quantity);
    }

    [Theory]
    [InlineData("cost?loaves=0")]
    [InlineData("cost?loaves=1001")]
    [InlineData("production-figures?loaves=0")]
    [InlineData("scaled?loaves=-1")]
    public async Task InvalidLoaves_Returns400(string query)
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);

        using var response = await anna.GetAsync($"{Recipe(Seed.R1)}/{query}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static decimal? Amount(CostBreakdownDto cost, string type) => cost.Lines.Single(l => l.Type == type).Amount;
}
