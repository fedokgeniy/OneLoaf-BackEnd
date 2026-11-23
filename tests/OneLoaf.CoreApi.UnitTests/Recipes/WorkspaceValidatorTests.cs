using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Recipes;

namespace OneLoaf.CoreApi.UnitTests.Recipes;

public sealed class WorkspaceValidatorTests
{
    [Fact]
    public void Kitchen_ValidAndInvalid()
    {
        var validator = new KitchenSettingsRequestValidator();

        Assert.True(validator.Validate(new KitchenSettingsRequest(2.5m, 4, 4, 0.25m, 0.15m, 10, 12, "EUR", "Europe/Vilnius")).IsValid);
        var invalid = validator.Validate(new KitchenSettingsRequest(0, 0, -1, -1, -1, 101, -1, "EURO", "Nowhere/City"));
        Assert.Equal(
            ["Currency", "ElectricityTariffPerKwh", "LaborRatePerHour", "LossRatePercent", "MixerBowlCapacityKg",
             "OvenCapacityLoaves", "OvenPowerKw", "PackagingCostPerLoaf", "TimeZone"],
            invalid.Errors.Select(e => e.PropertyName).Distinct().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Catalog_Rules()
    {
        Assert.False(new FlourRequestValidator().Validate(new FlourRequest(" ", "", 101, new string('g', 51), null)).IsValid);
        Assert.True(new FlourRequestValidator().Validate(new FlourRequest("Rye", "rye", 9, "fine", null)).IsValid);
        Assert.False(new IngredientRequestValidator().Validate(new IngredientRequest("", "", "", Guid.Empty)).IsValid);
        Assert.True(new IngredientRequestValidator().Validate(new IngredientRequest("Salt", "other", "g", null)).IsValid);
        Assert.False(new PriceRequestValidator().Validate(new PriceRequest(-1, 0, "EU", default)).IsValid);
        Assert.True(new PriceRequestValidator().Validate(new PriceRequest(1.2m, 1000, "EUR", new DateOnly(2026, 1, 1))).IsValid);
        Assert.False(new FlourListQueryValidator().Validate(new FlourListQuery(null, 0, 101)).IsValid);
        Assert.False(new IngredientListQueryValidator().Validate(new IngredientListQuery(null, null, 1, 0)).IsValid);
        Assert.True(new IngredientListQueryValidator().Validate(new IngredientListQuery()).IsValid);
    }

    [Fact]
    public void Recipe_ValidRequest_Passes() =>
        Assert.True(new RecipeRequestValidator().Validate(new RecipeRequest("Loaf", null, 1, 900, null,
            [new(null, Guid.NewGuid(), 500)], [new(null, "Mix", "mix", 10, 15, 10)])).IsValid);

    [Fact]
    public void Recipe_InvalidStageAndLineValues_Fail()
    {
        var result = new RecipeRequestValidator().Validate(new RecipeRequest("", null, 0, 0, null,
            [new(null, Guid.Empty, -1, SortOrder: -1)],
            [new(null, "", "", 20, 10, 30, TemperatureC: 400)]));

        var properties = result.Errors.Select(e => e.PropertyName).ToList();
        Assert.Contains("Title", properties);
        Assert.Contains("YieldLoaves", properties);
        Assert.Contains("LoafWeightG", properties);
        Assert.Contains("Ingredients[0].IngredientId", properties);
        Assert.Contains("Ingredients[0].Quantity", properties);
        Assert.Contains("Stages[0].MaxDurationMinutes", properties);
        Assert.Contains("Stages[0].ActiveMinutes", properties);
        Assert.Contains("Stages[0].TemperatureC", properties);
    }

    [Fact]
    public void Recipe_DuplicateLineIds_Fail()
    {
        var id = Guid.NewGuid();
        var result = new RecipeRequestValidator().Validate(new RecipeRequest("Loaf", null, 1, null, null,
            [new(id, Guid.NewGuid(), 1), new(id, Guid.NewGuid(), 2)], [new(id, "A", "mix", 1, 2), new(id, "B", "mix", 1, 2)]));

        Assert.Equal(2, result.Errors.Count(e => e.ErrorMessage == "Line ids must be unique."));
    }

    [Fact]
    public void Queries_Rules()
    {
        Assert.False(new LoavesQueryValidator().Validate(new LoavesQuery(0)).IsValid);
        Assert.True(new LoavesQueryValidator().Validate(new LoavesQuery(1000)).IsValid);
        Assert.False(new CostQueryValidator().Validate(new CostQuery(1001)).IsValid);
        Assert.False(new TimelineQueryValidator().Validate(new TimelineQuery(default)).IsValid);
        Assert.False(new StartWindowQueryValidator().Validate(new StartWindowQuery(default)).IsValid);
        Assert.True(new StartWindowQueryValidator().Validate(new StartWindowQuery(DateTimeOffset.UnixEpoch.AddDays(1))).IsValid);
        Assert.False(new RecipeListQueryValidator().Validate(new RecipeListQuery(MinProteinPercent: 120)).IsValid);
        Assert.False(new CreateVariantRequestValidator().Validate(new CreateVariantRequest(" ")).IsValid);
    }
}
