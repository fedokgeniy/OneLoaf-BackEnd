using FluentValidation;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.CoreApi.Application.Warnings;
using OneLoaf.CoreApi.UnitTests.Fakes;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.UnitTests.Recipes;

public sealed class WorkspaceServiceTests
{
    private static readonly Guid Me = Guid.NewGuid();
    private static readonly Guid Other = Guid.NewGuid();

    private readonly FakeFlourRepository _flours = new();
    private readonly FakeIngredientRepository _ingredients = new();
    private readonly FakeRecipeRepository _recipes = new();
    private readonly FakeKitchenRepository _kitchen = new();
    private readonly ReferenceGuard _reference = new(new FakeReferenceReader());
    private readonly FakeCurrentUser _user = new(Me);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ---- Kitchen ----

    [Fact]
    public async Task Kitchen_UpdateWithKnownCurrency_ReturnsStoredSettings()
    {
        var service = new KitchenSettingsService(_kitchen, _reference, _user);

        var result = await service.UpdateAsync(new KitchenSettingsRequest(2.5m, 4, 4, 0.25m, 0.15m, 10, 12, "USD", "UTC"), Ct);

        Assert.Equal("USD", result.Currency);
        Assert.Equal(2.5m, result.OvenPowerKw);
    }

    [Fact]
    public async Task Kitchen_UnknownCurrency_ThrowsValidation()
    {
        var service = new KitchenSettingsService(_kitchen, _reference, _user);

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            service.UpdateAsync(new KitchenSettingsRequest(null, null, null, null, 0, 0, null, "XXX", "UTC"), Ct));

        Assert.Equal("currency", Assert.Single(exception.Errors).PropertyName);
    }

    [Fact]
    public async Task Kitchen_MissingRow_ThrowsNotFound()
    {
        _kitchen.Settings = null;
        var service = new KitchenSettingsService(_kitchen, _reference, _user);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetAsync(Ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(new KitchenSettingsRequest(null, null, null, null, 0, 0, null, "EUR", "UTC"), Ct));
        Assert.Equal(TimeZoneInfo.Utc, await service.GetTimeZoneAsync(Ct));
    }

    // ---- Flours ----

    [Fact]
    public async Task Flour_CreateUpdateDelete()
    {
        var service = new FlourService(_flours, _reference, _user);

        var created = await service.CreateAsync(new FlourRequest(" Rye ", "rye", 9, null, null), Ct);
        var updated = await service.UpdateAsync(created.Id, new FlourRequest("Rye T1150", "rye", 9.5m, "medium", null), Ct);
        var page = await service.ListAsync(new FlourListQuery(), Ct);
        await service.DeleteAsync(created.Id, Ct);

        Assert.Equal("Rye", created.Name);
        Assert.Equal("Rye T1150", updated.Name);
        Assert.Equal(1, page.TotalCount);
        Assert.Empty(_flours.Flours);
    }

    [Fact]
    public async Task Flour_DuplicateNameOrUnknownTypeOrForeign_Fails()
    {
        var service = new FlourService(_flours, _reference, _user);
        var created = await service.CreateAsync(new FlourRequest("Rye", "rye", null, null, null), Ct);

        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new FlourRequest("Rye", "rye", null, null, null), Ct));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new FlourRequest("Spelt", "spelt", null, null, null), Ct));

        var foreign = new FlourService(_flours, _reference, new FakeCurrentUser(Other));
        await Assert.ThrowsAsync<NotFoundException>(() => foreign.GetAsync(created.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => foreign.UpdateAsync(created.Id, new FlourRequest("X", "rye", null, null, null), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => foreign.DeleteAsync(created.Id, Ct));
    }

    // ---- Ingredients and prices ----

    [Fact]
    public async Task Ingredient_CreateWithOwnFlourAndUpdate()
    {
        var flourId = await _flours.CreateAsync(Me, new FlourRequest("Bread flour", "wheat", 12.5m, null, null), Ct);
        var service = Ingredients(_user);

        var created = await service.CreateAsync(new IngredientRequest(" Bread flour ", "flour", "g", flourId), Ct);
        var updated = await service.UpdateAsync(created.Id, new IngredientRequest("Strong flour", "flour", "g", flourId), Ct);
        var list = await service.ListAsync(new IngredientListQuery(), Ct);

        Assert.Equal("Bread flour", created.Name);
        Assert.Equal("Strong flour", updated.Name);
        Assert.Equal(1, list.TotalCount);
    }

    [Fact]
    public async Task Ingredient_ForeignFlourUnknownCodesOrDuplicate_Fail()
    {
        var foreignFlour = await _flours.CreateAsync(Other, new FlourRequest("Theirs", "wheat", null, null, null), Ct);
        _ingredients.Add(Me, "Salt");
        var service = Ingredients(_user);

        var flourError = await Assert.ThrowsAsync<ValidationException>(() =>
            service.CreateAsync(new IngredientRequest("Flour", "flour", "g", foreignFlour), Ct));
        Assert.Equal("flourId", Assert.Single(flourError.Errors).PropertyName);
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new IngredientRequest("A", "spice", "g", null), Ct));
        await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(new IngredientRequest("A", "flour", "lb", null), Ct));
        await Assert.ThrowsAsync<ConflictException>(() => service.CreateAsync(new IngredientRequest("Salt", "flour", "g", null), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.UpdateAsync(Guid.NewGuid(), new IngredientRequest("B", "flour", "g", null), Ct));
    }

    [Fact]
    public async Task Ingredient_DeleteUsedOrMissing_Fails()
    {
        var used = _ingredients.Add(Me, "Flour");
        _ingredients.Used.Add(used);
        var unused = _ingredients.Add(Me, "Seeds");
        var service = Ingredients(_user);

        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(used, Ct));
        await service.DeleteAsync(unused, Ct);
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(unused, Ct));
    }

    [Fact]
    public async Task US12_AddPrices_KeepsHistoryAndRejectsSameDate()
    {
        var id = _ingredients.Add(Me, "Flour");
        var service = Ingredients(_user);

        await service.AddPriceAsync(id, new PriceRequest(1.20m, 1000, "EUR", new DateOnly(2026, 6, 1)), Ct);
        var newest = await service.AddPriceAsync(id, new PriceRequest(1.35m, 1000, "EUR", new DateOnly(2026, 10, 1)), Ct);
        var history = await service.GetPricesAsync(id, Ct);

        Assert.Equal(12.5m, newest.ChangePercent);
        Assert.Equal(2, history.Count);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddPriceAsync(id, new PriceRequest(1.40m, 1000, "EUR", new DateOnly(2026, 10, 1)), Ct));
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.AddPriceAsync(id, new PriceRequest(1.40m, 1000, "XXX", new DateOnly(2026, 11, 1)), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPricesAsync(Guid.NewGuid(), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.AddPriceAsync(Guid.NewGuid(), new PriceRequest(1, 1, "EUR", new DateOnly(2026, 1, 1)), Ct));
    }

    [Fact]
    public async Task US12_UpdateAndDeletePrice_CorrectAMistakeAndKeepOneDatePerPrice()
    {
        var id = _ingredients.Add(Me, "Flour");
        var service = Ingredients(_user);
        var june = await service.AddPriceAsync(id, new PriceRequest(1.20m, 1000, "EUR", new DateOnly(2026, 6, 1)), Ct);
        var october = await service.AddPriceAsync(id, new PriceRequest(13.5m, 1000, "EUR", new DateOnly(2026, 10, 1)), Ct);

        var corrected = await service.UpdatePriceAsync(id, october.Id, new PriceRequest(1.35m, 1000, "EUR", new DateOnly(2026, 10, 1)), Ct);
        Assert.Equal(12.5m, corrected.ChangePercent);
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdatePriceAsync(id, october.Id, new PriceRequest(1.35m, 1000, "EUR", new DateOnly(2026, 6, 1)), Ct));

        await service.DeletePriceAsync(id, june.Id, Ct);
        Assert.Null(Assert.Single(await service.GetPricesAsync(id, Ct)).ChangePercent);
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeletePriceAsync(id, june.Id, Ct));
    }

    // ---- Stock ----

    [Fact]
    public async Task Stock_Record_AddsPacksTimesPackSizeUnlessItIsPriceOnly()
    {
        var id = _ingredients.Add(Me, "Flour");
        var salt = _ingredients.Add(Me, "Salt");

        var bought = await Stock().AddRecordAsync(id, new IngredientRecordRequest(5, 1000, 6m, "EUR", new DateOnly(2026, 11, 20), "  "), Ct);
        var priced = await Stock().AddRecordAsync(salt, new IngredientRecordRequest(1, 1000, 0.8m, "EUR", new DateOnly(2026, 11, 20), PriceOnly: true), Ct);

        Assert.Equal(5000m, bought.Stock);
        Assert.True(bought.TrackStock);
        Assert.Null(_ingredients.LastRecord!.Note);
        Assert.Equal(0.8m, priced.CurrentPrice!.Price);
        Assert.False(priced.TrackStock);
        Assert.Null(priced.Stock);
        await Assert.ThrowsAsync<ValidationException>(() =>
            Stock().AddRecordAsync(id, new IngredientRecordRequest(1, 1, 1, "XXX", new DateOnly(2026, 11, 20)), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            Stock().AddRecordAsync(_ingredients.Add(Other, "Flour"), new IngredientRecordRequest(1, 1, 1, "EUR", new DateOnly(2026, 11, 20)), Ct));
    }

    [Fact]
    public async Task Stock_Adjust_WritesTheDifferenceToTheCountedQuantity()
    {
        var id = _ingredients.Add(Me, "Flour");
        _ingredients.Stock[id] = 5000m;

        var movement = await Stock().AdjustAsync(id, new StockAdjustmentRequest(4200m, " spilled "), Ct);

        Assert.Equal(-800m, movement.Quantity);
        Assert.Equal("spilled", movement.Note);
        Assert.Equal(new DateOnly(2026, 11, 24), movement.OccurredOn);
        var same = await Assert.ThrowsAsync<ValidationException>(() => Stock().AdjustAsync(id, new StockAdjustmentRequest(4200m, "again"), Ct));
        Assert.Equal(StockService.NothingToAdjustMessage, Assert.Single(same.Errors).ErrorMessage);
    }

    [Fact]
    public void PriceHistory_ChangeOnlyAgainstSameCurrencyAndNonZero()
    {
        var rows = new[]
        {
            new PriceRow(Guid.NewGuid(), 0m, 1, "EUR", new DateOnly(2026, 1, 1)),
            new PriceRow(Guid.NewGuid(), 2m, 1, "EUR", new DateOnly(2026, 2, 1)),
            new PriceRow(Guid.NewGuid(), 3m, 1, "USD", new DateOnly(2026, 3, 1)),
            new PriceRow(Guid.NewGuid(), 1.5m, 0.5m, "USD", new DateOnly(2026, 4, 1)),
        };

        var history = PriceHistory.WithChanges(rows);

        Assert.Equal([new DateOnly(2026, 4, 1), new DateOnly(2026, 3, 1), new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)],
            history.Select(p => p.EffectiveFrom));
        Assert.Equal([0m, null, null, null], history.Select(p => p.ChangePercent));
        Assert.Equal(3m, history[0].UnitPrice);
    }

    // ---- Recipes ----

    [Fact]
    public async Task Recipe_Create_ValidatesStagesAndIngredientOwnership()
    {
        var mine = _ingredients.Add(Me, "Flour");
        var theirs = _ingredients.Add(Other, "Flour");
        var service = Recipes();

        var created = await service.CreateAsync(new RecipeRequest("Loaf", null, 1, null, null,
            [new(null, mine, 500)], [new(null, "Bake", "bake", 40, 45)]), Ct);
        Assert.Equal("Loaf", created.Title);
        Assert.True(Assert.Single(_recipes.Created).Changes.Stages[0].UsesOven);

        var foreign = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new RecipeRequest("Loaf", null, 1, null, null, [new(null, theirs, 500)], []), Ct));
        Assert.Equal("ingredients[0].ingredientId", Assert.Single(foreign.Errors).PropertyName);
        var unknownStage = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new RecipeRequest("Loaf", null, 1, null, null, [], [new(null, "Rest", "rest", 1, 2)]), Ct));
        Assert.Equal("stages[0].stageTypeCode", Assert.Single(unknownStage.Errors).PropertyName);
        var proof = await Assert.ThrowsAsync<ValidationException>(() => service.CreateAsync(
            new RecipeRequest("Loaf", null, 1, null, null, [], [new(null, "Bake", "bake", 1, 2, ProofMode: ProofMode.Warm)]), Ct));
        Assert.Equal(RecipeService.ProofModeMessage, Assert.Single(proof.Errors).ErrorMessage);
    }

    [Fact]
    public async Task Recipe_CreateDraft_ReusesIngredientsByNameAndCreatesTheMissingOnes()
    {
        var flour = _ingredients.Add(Me, "Bread flour");
        var service = Recipes();

        var result = await service.CreateDraftAsync(new RecipeDraftRequest("Beginner loaf", null, 1, 900, null,
            [new("bread FLOUR", "flour", "g", 500), new("Water", "liquid", "g", 350)], [new(null, "Bake", "bake", 40, 45)]), Ct);

        Assert.Equal(["Water"], result.CreatedIngredients);
        var lines = Assert.Single(_recipes.Created).Changes.Ingredients;
        Assert.Equal(flour, lines[0].IngredientId);
        Assert.Null(_ingredients.Ingredients[lines[1].IngredientId].Ingredient.CurrentPrice);
        var unknownUnit = await Assert.ThrowsAsync<ValidationException>(() => service.CreateDraftAsync(
            new RecipeDraftRequest("Loaf", null, 1, null, null, [new("Salt", "flour", "cup", 10)], [new(null, "Bake", "bake", 40, 45)]), Ct));
        Assert.Equal("ingredients[0].unitCode", Assert.Single(unknownUnit.Errors).PropertyName);
    }

    [Fact]
    public async Task Recipe_UpdateBaseAndVariant()
    {
        var flour = _ingredients.Add(Me, "Flour");
        var baseId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var parent = FakeRecipeRepository.Recipe(baseId, null, "Loaf", 1,
            [new(lineId, flour, "Flour", "flour", "g", 500, false, 1, null, null, false)]);
        _recipes.Add(Me, parent, new RecipeState(baseId, null, 1, [new(lineId, flour, 500, false, 1, null, null, false)], []));
        var variantId = Guid.NewGuid();
        _recipes.Add(Me, FakeRecipeRepository.Recipe(variantId, baseId, "Variant", 1));
        var service = Recipes();

        await service.UpdateAsync(baseId, new RecipeRequest("Loaf 2", null, 1, null, null, [new(lineId, flour, 550)], []), Ct);
        await service.UpdateAsync(variantId, new RecipeRequest("Variant", null, 1, null, null, [new(lineId, flour, 600, SortOrder: 1)], []), Ct);

        Assert.Equal(lineId, _recipes.Saved[0].Changes.Ingredients[0].Id);
        Assert.Equal(lineId, Assert.Single(_recipes.Saved[1].Changes.Ingredients).ReplacesItemId);
        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.UpdateAsync(Guid.NewGuid(), new RecipeRequest("X", null, 1, null, null, [], []), Ct));
    }

    [Fact]
    public async Task Recipe_VariantArchiveDeleteScaleCompare()
    {
        var flour = _ingredients.Add(Me, "Flour");
        var baseId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        _recipes.Add(Me, FakeRecipeRepository.Recipe(baseId, null, "Loaf", 1,
            [new(lineId, flour, "Flour", "flour", "g", 500, false, 1, null, null, false)]));
        var service = Recipes();

        var variant = await service.CreateVariantAsync(baseId, new CreateVariantRequest("Variant"), Ct);
        Assert.Equal(baseId, Assert.Single(_recipes.Created).Parent);

        await service.SetArchivedAsync(baseId, true, Ct);
        Assert.True((await service.GetAsync(baseId, Ct)).IsArchived);
        Assert.Equal(2, (await service.ListAsync(new RecipeListQuery(), Ct)).TotalCount);

        var scaled = await service.GetScaledAsync(baseId, 3, Ct);
        Assert.Equal(1500m, scaled.Ingredients[0].Quantity);

        var comparison = await service.GetComparisonAsync(variant.Id, Ct);
        Assert.Equal(baseId, comparison.ParentRecipeId);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetComparisonAsync(baseId, Ct));

        _recipes.Referenced.Add(baseId);
        await Assert.ThrowsAsync<ConflictException>(() => service.DeleteAsync(baseId, Ct));
        await service.DeleteAsync(variant.Id, Ct);
        await Assert.ThrowsAsync<NotFoundException>(() => service.DeleteAsync(variant.Id, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.SetArchivedAsync(variant.Id, false, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetComparisonAsync(variant.Id, Ct));
    }

    // ---- Calculations ----

    [Fact]
    public async Task Cost_MapsLinesTotalAndMissingPrices()
    {
        var recipeId = Guid.NewGuid();
        var missing = Guid.NewGuid();
        _recipes.Add(Me, FakeRecipeRepository.Recipe(recipeId, null, "Loaf", 1));
        var calculations = new FakeCalculations
        {
            Cost =
            [
                new(1, CostLineTypes.Ingredient, missing, "Rye flour", 450, "g", null, null, "EUR", false),
                new(2, CostLineTypes.Packaging, null, "Packaging", 1, "pcs", 0, 0, "EUR", true),
                new(3, CostLineTypes.Total, null, "Total", 1, "loaves", null, 0, "EUR", false),
            ],
        };
        var service = Calculations(calculations);

        var cost = await service.GetCostAsync(recipeId, new CostQuery(1, new DateOnly(2026, 11, 15)), Ct);

        Assert.False(cost.IsComplete);
        Assert.Equal("EUR", cost.Currency);
        Assert.Equal(0m, cost.Total);
        Assert.Equal(new MissingPriceDto(missing, "Rye flour"), Assert.Single(cost.MissingPrices));
        Assert.Equal(new DateOnly(2026, 11, 15), calculations.LastAsOf);
    }

    [Fact]
    public async Task Cost_WithoutDate_UsesTodayInTheKitchenTimeZone()
    {
        var recipeId = Guid.NewGuid();
        _recipes.Add(Me, FakeRecipeRepository.Recipe(recipeId, null, "Loaf", 1));
        _kitchen.Settings = _kitchen.Settings! with { TimeZone = "Pacific/Kiritimati" };
        var calculations = new FakeCalculations();
        var service = Calculations(calculations, new DateTimeOffset(2026, 11, 15, 20, 0, 0, TimeSpan.Zero));

        var cost = await service.GetCostAsync(recipeId, new CostQuery(1), Ct);

        Assert.Equal(new DateOnly(2026, 11, 16), cost.AsOf);
        Assert.Equal(string.Empty, cost.Currency);
        Assert.Null(cost.Total);
    }

    [Fact]
    public async Task Calculator_OtherUsersRecipe_ThrowsNotFound()
    {
        var recipeId = Guid.NewGuid();
        _recipes.Add(Other, FakeRecipeRepository.Recipe(recipeId, null, "Theirs", 1));
        var service = Calculations(new FakeCalculations());
        var start = DateTimeOffset.UnixEpoch;

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetCostAsync(recipeId, new CostQuery(), Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetTimelineAsync(recipeId, start, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetStartWindowAsync(recipeId, start, Ct));
        await Assert.ThrowsAsync<NotFoundException>(() => service.GetProductionFiguresAsync(recipeId, 1, Ct));
    }

    [Fact]
    public async Task Calculator_TimelineWindowsAndFigures_ComeFromTheGateway()
    {
        var recipeId = Guid.NewGuid();
        _recipes.Add(Me, FakeRecipeRepository.Recipe(recipeId, null, "Loaf", 1));
        var service = Calculations(new FakeCalculations());
        var start = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);

        var timeline = await service.GetTimelineAsync(recipeId, start, Ct);
        var window = await service.GetStartWindowAsync(recipeId, start, Ct);
        var figures = await service.GetProductionFiguresAsync(recipeId, 3, Ct);

        Assert.Equal(start.AddMinutes(45), timeline.ReadyTo);
        Assert.Single(timeline.Stages);
        Assert.Equal(start.AddMinutes(-45), window.StartFrom);
        Assert.Equal(300m, figures.StarterAmountG);
    }

    [Fact]
    public async Task Warnings_AreReadForTheCurrentUser()
    {
        var reader = new RecordingWarningsReader();

        var warnings = await new WarningsService(reader, _user).GetAsync(Ct);

        Assert.Equal(Me, reader.Owner);
        Assert.Single(warnings);
    }

    private IngredientService Ingredients(FakeCurrentUser user) => new(_ingredients, _flours, _reference, user);

    private RecipeService Recipes() => new(_recipes, _ingredients, _reference, new ImmediateTransaction(), _user);

    private StockService Stock() =>
        new(_ingredients, _reference, new FixedTimeProvider(new DateTimeOffset(2026, 11, 24, 8, 0, 0, TimeSpan.Zero)), _user);

    private CalculationService Calculations(FakeCalculations calculations, DateTimeOffset? now = null) =>
        new(calculations, _recipes, new KitchenSettingsService(_kitchen, _reference, _user),
            new FixedTimeProvider(now ?? DateTimeOffset.UtcNow), _user);

    private sealed class RecordingWarningsReader : IWarningsReader
    {
        public Guid Owner { get; private set; }

        public Task<IReadOnlyList<WarningDto>> GetAsync(Guid ownerId, CancellationToken cancellationToken)
        {
            Owner = ownerId;
            return Task.FromResult<IReadOnlyList<WarningDto>>([new("missing_price", "ingredient", Guid.NewGuid(), "No price")]);
        }
    }
}
