using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Recipes;
using OneLoaf.CoreApi.Application.Reference;

namespace OneLoaf.CoreApi.UnitTests.Fakes;

internal sealed class FakeReferenceReader : IReferenceDataReader
{
    public Task<IReadOnlyList<UnitDto>> GetUnitsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UnitDto>>([new("g", "gram", "mass", 1), new("kg", "kilogram", "mass", 1000)]);

    public Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CurrencyDto>>([new("EUR", "Euro", 2), new("USD", "US dollar", 2)]);

    public Task<IReadOnlyList<StageTypeDto>> GetStageTypesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StageTypeDto>>(
        [
            new("mix", "Mixing", false, false, true),
            new("proof", "Proofing", true, false, false),
            new("bake", "Baking", false, true, false),
        ]);

    public Task<IReadOnlyList<IngredientCategoryDto>> GetIngredientCategoriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<IngredientCategoryDto>>([new("flour", "Flour", true, false), new("liquid", "Liquid", false, true)]);

    public Task<IReadOnlyList<FlourTypeDto>> GetFlourTypesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FlourTypeDto>>([new("wheat", "Wheat"), new("rye", "Rye")]);
}

internal sealed class FakeKitchenRepository : IKitchenSettingsRepository
{
    public KitchenSettingsDto? Settings { get; set; } =
        new(null, null, null, null, 0, 0, null, "EUR", "Europe/Vilnius", DateTimeOffset.UnixEpoch);

    public Task<KitchenSettingsDto?> GetAsync(Guid ownerId, CancellationToken cancellationToken) => Task.FromResult(Settings);

    public Task<bool> UpdateAsync(Guid ownerId, KitchenSettingsRequest settings, CancellationToken cancellationToken)
    {
        if (Settings is null)
        {
            return Task.FromResult(false);
        }

        Settings = new KitchenSettingsDto(settings.OvenPowerKw, settings.OvenCapacityLoaves, settings.MixerBowlCapacityKg,
            settings.ElectricityTariffPerKwh, settings.PackagingCostPerLoaf, settings.LossRatePercent,
            settings.LaborRatePerHour, settings.Currency, settings.TimeZone, DateTimeOffset.UnixEpoch);
        return Task.FromResult(true);
    }
}

internal sealed class FakeFlourRepository : IFlourRepository
{
    public Dictionary<Guid, (Guid Owner, FlourDto Flour)> Flours { get; } = [];

    public Task<PagedResult<FlourDto>> ListAsync(Guid ownerId, FlourListQuery query, CancellationToken cancellationToken)
    {
        var items = Flours.Values.Where(f => f.Owner == ownerId).Select(f => f.Flour).ToList();
        return Task.FromResult(new PagedResult<FlourDto>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<FlourDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Flours.TryGetValue(id, out var f) && f.Owner == ownerId ? f.Flour : null);

    public Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(Flours.Values.Any(f => f.Owner == ownerId && f.Flour.Name == name && f.Flour.Id != exceptId));

    public Task<Guid> CreateAsync(Guid ownerId, FlourRequest flour, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        Flours[id] = (ownerId, ToDto(id, flour));
        return Task.FromResult(id);
    }

    public Task<bool> UpdateAsync(Guid ownerId, Guid id, FlourRequest flour, CancellationToken cancellationToken)
    {
        if (!Flours.TryGetValue(id, out var f) || f.Owner != ownerId)
        {
            return Task.FromResult(false);
        }

        Flours[id] = (ownerId, ToDto(id, flour));
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Flours.TryGetValue(id, out var f) && f.Owner == ownerId && Flours.Remove(id));

    private static FlourDto ToDto(Guid id, FlourRequest f) =>
        new(id, f.Name, f.FlourTypeCode, f.ProteinPercent, f.Grind, f.Notes, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}

internal sealed class FakeIngredientRepository : IIngredientRepository
{
    public Dictionary<Guid, (Guid Owner, IngredientDto Ingredient)> Ingredients { get; } = [];

    public Dictionary<Guid, List<PriceRow>> Prices { get; } = [];

    public HashSet<Guid> Used { get; } = [];

    public Guid Add(Guid ownerId, string name)
    {
        var id = Guid.NewGuid();
        Ingredients[id] = (ownerId, new IngredientDto(id, name, "flour", "g", null, null, null, null,
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch));
        Prices[id] = [];
        return id;
    }

    public Task<PagedResult<IngredientDto>> ListAsync(Guid ownerId, IngredientListQuery query, CancellationToken cancellationToken)
    {
        var items = Ingredients.Values.Where(i => i.Owner == ownerId).Select(i => i.Ingredient).ToList();
        return Task.FromResult(new PagedResult<IngredientDto>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<IngredientDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Ingredients.TryGetValue(id, out var i) && i.Owner == ownerId ? i.Ingredient : null);

    public Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        Task.FromResult(Ingredients.Values.Any(i => i.Owner == ownerId && i.Ingredient.Name == name && i.Ingredient.Id != exceptId));

    public Task<IReadOnlySet<Guid>> GetOwnedIdsAsync(Guid ownerId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(ids.Where(id => Ingredients.TryGetValue(id, out var i) && i.Owner == ownerId).ToHashSet());

    public Task<bool> IsUsedAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) => Task.FromResult(Used.Contains(id));

    public Task<IReadOnlyDictionary<string, Guid>> FindByNamesAsync(
        Guid ownerId, IReadOnlyCollection<string> names, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, Guid>>(Ingredients.Values
            .Where(i => i.Owner == ownerId && names.Contains(i.Ingredient.Name, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(i => i.Ingredient.Name, i => i.Ingredient.Id, StringComparer.OrdinalIgnoreCase));

    public Task<Guid> CreateAsync(Guid ownerId, IngredientRequest ingredient, CancellationToken cancellationToken) =>
        Task.FromResult(Add(ownerId, ingredient.Name));

    public Task<bool> UpdateAsync(Guid ownerId, Guid id, IngredientRequest ingredient, CancellationToken cancellationToken)
    {
        if (!Ingredients.TryGetValue(id, out var i) || i.Owner != ownerId)
        {
            return Task.FromResult(false);
        }

        Ingredients[id] = (ownerId, i.Ingredient with { Name = ingredient.Name, FlourId = ingredient.FlourId });
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Ingredients.TryGetValue(id, out var i) && i.Owner == ownerId && Ingredients.Remove(id));

    public Task<IReadOnlyList<PriceRow>?> GetPricesAsync(Guid ownerId, Guid ingredientId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PriceRow>?>(
            Ingredients.TryGetValue(ingredientId, out var i) && i.Owner == ownerId ? Prices[ingredientId] : null);

    public Task<Guid> AddPriceAsync(Guid ingredientId, PriceRequest price, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        Prices[ingredientId].Add(new PriceRow(id, price.Price, price.PerQuantity, price.Currency, price.EffectiveFrom));
        return Task.FromResult(id);
    }

    public Dictionary<Guid, decimal> Stock { get; } = [];

    public List<StockMovementDto> Movements { get; } = [];

    public IngredientRecordRequest? LastRecord { get; private set; }

    public Task<bool> UpdatePriceAsync(Guid ingredientId, Guid priceId, PriceRequest price, CancellationToken cancellationToken)
    {
        var index = Prices[ingredientId].FindIndex(p => p.Id == priceId);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        Prices[ingredientId][index] = new PriceRow(priceId, price.Price, price.PerQuantity, price.Currency, price.EffectiveFrom);
        return Task.FromResult(true);
    }

    public Task<bool> DeletePriceAsync(Guid ingredientId, Guid priceId, CancellationToken cancellationToken) =>
        Task.FromResult(Prices[ingredientId].RemoveAll(p => p.Id == priceId) > 0);

    public Task<decimal> GetStockAsync(Guid ingredientId, CancellationToken cancellationToken) =>
        Task.FromResult(Stock.GetValueOrDefault(ingredientId));

    public Task<PagedResult<StockMovementDto>?> ListMovementsAsync(
        Guid ownerId, Guid ingredientId, StockMovementListQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Ingredients.TryGetValue(ingredientId, out var i) && i.Owner == ownerId
            ? new PagedResult<StockMovementDto>(Movements, query.Page, query.PageSize, Movements.Count)
            : null);

    public Task AddRecordAsync(Guid ingredientId, IngredientRecordRequest record, CancellationToken cancellationToken)
    {
        LastRecord = record;
        var (owner, ingredient) = Ingredients[ingredientId];
        var quantity = record.PackCount * record.PackSize;
        var price = new CurrentPriceDto(record.TotalCost, quantity, record.TotalCost / quantity, record.Currency, record.Date);
        if (record.PriceOnly)
        {
            Ingredients[ingredientId] = (owner, ingredient with { CurrentPrice = price });
            return Task.CompletedTask;
        }

        Record(ingredientId, OneLoaf.Domain.StockMovementType.Purchase, quantity, record.Date, record.Note);
        Ingredients[ingredientId] = (owner, ingredient with { CurrentPrice = price, TrackStock = true, Stock = Stock[ingredientId] });
        return Task.CompletedTask;
    }

    public Task<Guid> AddAdjustmentAsync(
        Guid ingredientId, decimal quantity, DateOnly occurredOn, string note, CancellationToken cancellationToken) =>
        Task.FromResult(Record(ingredientId, OneLoaf.Domain.StockMovementType.Adjustment, quantity, occurredOn, note));

    public Task<StockMovementDto> GetMovementAsync(Guid movementId, CancellationToken cancellationToken) =>
        Task.FromResult(Movements.Single(m => m.Id == movementId));

    private Guid Record(Guid ingredientId, OneLoaf.Domain.StockMovementType type, decimal quantity, DateOnly on, string? note)
    {
        var id = Guid.NewGuid();
        Stock[ingredientId] = Stock.GetValueOrDefault(ingredientId) + quantity;
        Movements.Add(new StockMovementDto(id, type, quantity, on, null, null, null, null, null, null, note, DateTimeOffset.UnixEpoch));
        return id;
    }
}

internal sealed class FakeRecipeRepository : IRecipeRepository
{
    public Dictionary<Guid, (Guid Owner, RecipeDto Effective, RecipeState State)> Recipes { get; } = [];

    public HashSet<Guid> Referenced { get; } = [];

    public List<(Guid? Parent, RecipeChangeSet Changes)> Created { get; } = [];

    public List<(Guid Id, RecipeChangeSet Changes)> Saved { get; } = [];

    public void Add(Guid ownerId, RecipeDto effective, RecipeState? state = null) =>
        Recipes[effective.Id] = (ownerId, effective, state ?? new RecipeState(effective.Id, effective.ParentRecipeId,
            effective.YieldLoaves, [], []));

    public Task<PagedResult<RecipeSummaryDto>> ListAsync(Guid ownerId, RecipeListQuery query, CancellationToken cancellationToken)
    {
        var items = Recipes.Values.Where(r => r.Owner == ownerId)
            .Select(r => new RecipeSummaryDto(r.Effective.Id, r.Effective.Title, r.Effective.ParentRecipeId, r.Effective.YieldLoaves,
                r.Effective.IsArchived, r.Effective.UpdatedAt)).ToList();
        return Task.FromResult(new PagedResult<RecipeSummaryDto>(items, query.Page, query.PageSize, items.Count));
    }

    public Task<RecipeDto?> GetEffectiveAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Recipes.TryGetValue(id, out var r) && r.Owner == ownerId ? r.Effective : null);

    public Task<RecipeState?> GetStateAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Recipes.TryGetValue(id, out var r) && r.Owner == ownerId ? r.State : null);

    public Task<bool> ExistsAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Recipes.TryGetValue(id, out var r) && r.Owner == ownerId);

    public Task<Guid> CreateAsync(Guid ownerId, Guid? parentRecipeId, RecipeChangeSet recipe, CancellationToken cancellationToken)
    {
        Created.Add((parentRecipeId, recipe));
        var id = Guid.NewGuid();
        Add(ownerId, Recipe(id, parentRecipeId, recipe.Header.Title, recipe.Header.YieldLoaves));
        return Task.FromResult(id);
    }

    public Task<bool> SaveAsync(Guid ownerId, Guid id, RecipeChangeSet changes, CancellationToken cancellationToken)
    {
        Saved.Add((id, changes));
        return Task.FromResult(Recipes.TryGetValue(id, out var r) && r.Owner == ownerId);
    }

    public Task<bool> SetArchivedAsync(Guid ownerId, Guid id, bool archived, CancellationToken cancellationToken)
    {
        if (!Recipes.TryGetValue(id, out var r) || r.Owner != ownerId)
        {
            return Task.FromResult(false);
        }

        Recipes[id] = r with { Effective = r.Effective with { IsArchived = archived } };
        return Task.FromResult(true);
    }

    public Task<bool> IsReferencedAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Referenced.Contains(id));

    public Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Recipes.TryGetValue(id, out var r) && r.Owner == ownerId && Recipes.Remove(id));

    public static RecipeDto Recipe(
        Guid id, Guid? parentId, string title, int yieldLoaves,
        IReadOnlyList<RecipeIngredientDto>? ingredients = null, IReadOnlyList<RecipeStageDto>? stages = null,
        string? description = null, decimal? loafWeightG = null, string? notes = null) =>
        new(id, parentId, title, description, yieldLoaves, loafWeightG, notes, false, ingredients ?? [], stages ?? [],
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
}

internal sealed class FakeCalculations : IRecipeCalculations
{
    public IReadOnlyList<CostLine> Cost { get; set; } = [];

    public DateOnly? LastAsOf { get; private set; }

    public Task<IReadOnlyList<CostLine>> GetCostAsync(Guid recipeId, int loaves, DateOnly asOf, CancellationToken cancellationToken)
    {
        LastAsOf = asOf;
        return Task.FromResult(Cost);
    }

    public Task<IReadOnlyList<TimelineStageDto>> GetTimelineAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TimelineStageDto>>(
            [new(Guid.NewGuid(), 1, "Bake", "bake", 40, 45, start, start.AddMinutes(40), start.AddMinutes(45))]);

    public Task<TimeWindow> GetReadyWindowAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken) =>
        Task.FromResult(new TimeWindow(start.AddMinutes(40), start.AddMinutes(45)));

    public Task<TimeWindow> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken cancellationToken) =>
        Task.FromResult(new TimeWindow(readyAt.AddMinutes(-45), readyAt.AddMinutes(-40)));

    public Task<ProductionFigures> GetProductionFiguresAsync(Guid recipeId, int loaves, CancellationToken cancellationToken) =>
        Task.FromResult(new ProductionFigures(960m * loaves, 1, 100m * loaves));
}

/// <summary>Runs the work at once: the fakes have no transaction to roll back.</summary>
internal sealed class ImmediateTransaction : OneLoaf.CoreApi.Application.Common.IWorkspaceTransaction
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) => work(cancellationToken);
}
