using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Kitchen;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="IReferenceDataReader"/>.</summary>
public sealed class ReferenceDataReader(OneLoafDbContext db) : IReferenceDataReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<UnitDto>> GetUnitsAsync(CancellationToken cancellationToken) =>
        await db.Units.AsNoTracking().OrderBy(u => u.SortOrder)
            .Select(u => new UnitDto(u.Code, u.Name, u.Dimension, u.FactorToBase)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken) =>
        await db.Currencies.AsNoTracking().OrderBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Code, c.Name, c.MinorUnits)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<StageTypeDto>> GetStageTypesAsync(CancellationToken cancellationToken) =>
        await db.StageTypes.AsNoTracking().OrderBy(s => s.SortOrder)
            .Select(s => new StageTypeDto(s.Code, s.Name, s.IsFermentation, s.DefaultUsesOven, s.DefaultUsesMixer))
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IngredientCategoryDto>> GetIngredientCategoriesAsync(CancellationToken cancellationToken) =>
        await db.IngredientCategories.AsNoTracking().OrderBy(c => c.SortOrder)
            .Select(c => new IngredientCategoryDto(c.Code, c.Name, c.IsFlour, c.IsLiquid)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<FlourTypeDto>> GetFlourTypesAsync(CancellationToken cancellationToken) =>
        await db.FlourTypes.AsNoTracking().OrderBy(f => f.SortOrder)
            .Select(f => new FlourTypeDto(f.Code, f.Name)).ToListAsync(cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IKitchenSettingsRepository"/>.</summary>
public sealed class KitchenSettingsRepository(OneLoafDbContext db) : IKitchenSettingsRepository
{
    /// <inheritdoc />
    public Task<KitchenSettingsDto?> GetAsync(Guid ownerId, CancellationToken cancellationToken) =>
        db.KitchenSettings.AsNoTracking().Where(k => k.UserId == ownerId)
            .Select(k => new KitchenSettingsDto(k.OvenPowerKw, k.OvenCapacityLoaves, k.MixerBowlCapacityKg,
                k.ElectricityTariffPerKwh, k.PackagingCostPerLoaf, k.LossRatePercent, k.LaborRatePerHour, k.Currency,
                k.TimeZone, new DateTimeOffset(k.UpdatedAt, TimeSpan.Zero)))
            .SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid ownerId, KitchenSettingsRequest settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var updated = await db.KitchenSettings.Where(k => k.UserId == ownerId).ExecuteUpdateAsync(s => s
            .SetProperty(k => k.OvenPowerKw, settings.OvenPowerKw)
            .SetProperty(k => k.OvenCapacityLoaves, settings.OvenCapacityLoaves)
            .SetProperty(k => k.MixerBowlCapacityKg, settings.MixerBowlCapacityKg)
            .SetProperty(k => k.ElectricityTariffPerKwh, settings.ElectricityTariffPerKwh)
            .SetProperty(k => k.PackagingCostPerLoaf, settings.PackagingCostPerLoaf)
            .SetProperty(k => k.LossRatePercent, settings.LossRatePercent)
            .SetProperty(k => k.LaborRatePerHour, settings.LaborRatePerHour)
            .SetProperty(k => k.Currency, settings.Currency)
            .SetProperty(k => k.TimeZone, settings.TimeZone), cancellationToken);
        return updated > 0;
    }
}

/// <summary>EF Core implementation of <see cref="IFlourRepository"/>.</summary>
public sealed class FlourRepository(OneLoafDbContext db) : IFlourRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<FlourDto>> ListAsync(Guid ownerId, FlourListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var flours = db.Flours.AsNoTracking().Where(f => f.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            flours = flours.Where(f => EF.Functions.ILike(f.Name, Like(query.Search)));
        }

        var total = await flours.CountAsync(cancellationToken);
        var items = await Project(flours.OrderBy(f => f.Name).Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new PagedResult<FlourDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public Task<FlourDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        Project(db.Flours.AsNoTracking().Where(f => f.OwnerId == ownerId && f.Id == id)).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Flours.AsNoTracking().AnyAsync(f => f.OwnerId == ownerId && f.Name == name && f.Id != exceptId, cancellationToken);

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(Guid ownerId, FlourRequest flour, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flour);
        var entity = new Flour
        {
            OwnerId = ownerId,
            Name = flour.Name,
            FlourTypeCode = flour.FlourTypeCode,
            ProteinPercent = flour.ProteinPercent,
            Grind = flour.Grind,
            Notes = flour.Notes,
        };
        db.Flours.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid ownerId, Guid id, FlourRequest flour, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(flour);
        return await db.Flours.Where(f => f.OwnerId == ownerId && f.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(f => f.Name, flour.Name)
            .SetProperty(f => f.FlourTypeCode, flour.FlourTypeCode)
            .SetProperty(f => f.ProteinPercent, flour.ProteinPercent)
            .SetProperty(f => f.Grind, flour.Grind)
            .SetProperty(f => f.Notes, flour.Notes), cancellationToken) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.Flours.Where(f => f.OwnerId == ownerId && f.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    internal static string Like(string search) =>
        "%" + search.Trim().Replace(@"\", @"\\", StringComparison.Ordinal).Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal) + "%";

    private static IQueryable<FlourDto> Project(IQueryable<Flour> flours) =>
        flours.Select(f => new FlourDto(f.Id, f.Name, f.FlourTypeCode, f.ProteinPercent, f.Grind, f.Notes,
            new DateTimeOffset(f.CreatedAt, TimeSpan.Zero), new DateTimeOffset(f.UpdatedAt, TimeSpan.Zero)));
}

/// <summary>EF Core implementation of <see cref="IIngredientRepository"/>; today's price comes from <c>v_ingredient_current_prices</c>.</summary>
public sealed class IngredientRepository(OneLoafDbContext db) : IIngredientRepository
{
    /// <inheritdoc />
    public async Task<PagedResult<IngredientDto>> ListAsync(Guid ownerId, IngredientListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var ingredients = db.Ingredients.AsNoTracking().Where(i => i.OwnerId == ownerId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            ingredients = ingredients.Where(i => EF.Functions.ILike(i.Name, FlourRepository.Like(query.Search)));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            ingredients = ingredients.Where(i => i.CategoryCode == query.Category);
        }

        var total = await ingredients.CountAsync(cancellationToken);
        var rows = await Project(ingredients.OrderBy(i => i.Name).Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        var items = await WithCurrentPricesAsync(rows, cancellationToken);
        return new PagedResult<IngredientDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<IngredientDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var row = await Project(db.Ingredients.AsNoTracking().Where(i => i.OwnerId == ownerId && i.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (await WithCurrentPricesAsync([row], cancellationToken))[0];
    }

    /// <inheritdoc />
    public Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Ingredients.AsNoTracking().AnyAsync(i => i.OwnerId == ownerId && i.Name == name && i.Id != exceptId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlySet<Guid>> GetOwnedIdsAsync(Guid ownerId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var owned = await db.Ingredients.AsNoTracking().Where(i => i.OwnerId == ownerId && ids.Contains(i.Id))
            .Select(i => i.Id).ToListAsync(cancellationToken);
        return owned.ToHashSet();
    }

    /// <inheritdoc />
    public Task<bool> IsUsedAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        db.RecipeIngredients.AsNoTracking().AnyAsync(l => l.IngredientId == id && l.Recipe.OwnerId == ownerId, cancellationToken);

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(Guid ownerId, IngredientRequest ingredient, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        var entity = new Ingredient
        {
            OwnerId = ownerId,
            Name = ingredient.Name,
            CategoryCode = ingredient.CategoryCode,
            UnitCode = ingredient.UnitCode,
            FlourId = ingredient.FlourId,
        };
        db.Ingredients.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Guid ownerId, Guid id, IngredientRequest ingredient, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ingredient);
        return await db.Ingredients.Where(i => i.OwnerId == ownerId && i.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.Name, ingredient.Name)
            .SetProperty(i => i.CategoryCode, ingredient.CategoryCode)
            .SetProperty(i => i.UnitCode, ingredient.UnitCode)
            .SetProperty(i => i.FlourId, ingredient.FlourId), cancellationToken) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken) =>
        await db.Ingredients.Where(i => i.OwnerId == ownerId && i.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<IReadOnlyList<PriceRow>?> GetPricesAsync(Guid ownerId, Guid ingredientId, CancellationToken cancellationToken)
    {
        if (!await db.Ingredients.AsNoTracking().AnyAsync(i => i.OwnerId == ownerId && i.Id == ingredientId, cancellationToken))
        {
            return null;
        }

        return await db.IngredientPrices.AsNoTracking().Where(p => p.IngredientId == ingredientId)
            .OrderByDescending(p => p.EffectiveFrom)
            .Select(p => new PriceRow(p.Id, p.Price, p.PerQuantity, p.Currency, p.EffectiveFrom))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid> AddPriceAsync(Guid ingredientId, PriceRequest price, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(price);
        var entity = new IngredientPrice
        {
            IngredientId = ingredientId,
            Price = price.Price,
            PerQuantity = price.PerQuantity,
            Currency = price.Currency,
            EffectiveFrom = price.EffectiveFrom,
        };
        db.IngredientPrices.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static IQueryable<IngredientDto> Project(IQueryable<Ingredient> ingredients) =>
        ingredients.Select(i => new IngredientDto(i.Id, i.Name, i.CategoryCode, i.UnitCode, i.FlourId,
            i.Flour == null ? null : i.Flour.Name, i.Flour == null ? null : i.Flour.ProteinPercent, null,
            new DateTimeOffset(i.CreatedAt, TimeSpan.Zero), new DateTimeOffset(i.UpdatedAt, TimeSpan.Zero)));

    private async Task<List<IngredientDto>> WithCurrentPricesAsync(List<IngredientDto> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return rows;
        }

        var ids = rows.Select(r => r.Id).ToArray();
        var prices = await db.Database.SqlQuery<CurrentPriceRow>($"""
            SELECT ingredient_id AS "IngredientId", price AS "Price", per_quantity AS "PerQuantity",
                   unit_price AS "UnitPrice", currency AS "Currency", effective_from AS "EffectiveFrom"
            FROM v_ingredient_current_prices
            WHERE ingredient_id = ANY({ids}) AND price IS NOT NULL
            """).ToDictionaryAsync(p => p.IngredientId, cancellationToken);

        return rows.Select(r => prices.TryGetValue(r.Id, out var p)
            ? r with { CurrentPrice = new CurrentPriceDto(p.Price, p.PerQuantity, p.UnitPrice, p.Currency.Trim(), p.EffectiveFrom) }
            : r).ToList();
    }

    private sealed class CurrentPriceRow
    {
        public Guid IngredientId { get; set; }

        public decimal Price { get; set; }

        public decimal PerQuantity { get; set; }

        public decimal UnitPrice { get; set; }

        public string Currency { get; set; } = string.Empty;

        public DateOnly EffectiveFrom { get; set; }
    }
}
