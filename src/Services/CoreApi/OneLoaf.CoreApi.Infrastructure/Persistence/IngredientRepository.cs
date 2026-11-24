using Microsoft.EntityFrameworkCore;
using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;
using OneLoaf.Domain.Entities;
using OneLoaf.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Persistence;

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
        var items = await WithStockAsync(await WithCurrentPricesAsync(rows, cancellationToken), cancellationToken);
        return new PagedResult<IngredientDto>(items, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task<IngredientDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var row = await Project(db.Ingredients.AsNoTracking().Where(i => i.OwnerId == ownerId && i.Id == id))
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : (await WithStockAsync(await WithCurrentPricesAsync([row], cancellationToken), cancellationToken))[0];
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
    public async Task<IReadOnlyDictionary<string, Guid>> FindByNamesAsync(
        Guid ownerId, IReadOnlyCollection<string> names, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        // A catalog holds at most a few hundred ingredients, so the names are compared in memory without case.
        var wanted = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = await db.Ingredients.AsNoTracking().Where(i => i.OwnerId == ownerId)
            .Select(i => new { i.Name, i.Id }).ToListAsync(cancellationToken);
        var result = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var ingredient in all.Where(i => wanted.Contains(i.Name)))
        {
            result.TryAdd(ingredient.Name, ingredient.Id);
        }

        return result;
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
            TrackStock = ingredient.TrackStock ?? false,
            ReorderLevel = ingredient.ReorderLevel,
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
            .SetProperty(i => i.FlourId, ingredient.FlourId)
            .SetProperty(i => i.TrackStock, i => ingredient.TrackStock ?? i.TrackStock)
            .SetProperty(i => i.ReorderLevel, ingredient.ReorderLevel), cancellationToken) > 0;
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

    /// <inheritdoc />
    public async Task<bool> UpdatePriceAsync(Guid ingredientId, Guid priceId, PriceRequest price, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(price);
        return await db.IngredientPrices.Where(p => p.IngredientId == ingredientId && p.Id == priceId).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Price, price.Price)
            .SetProperty(p => p.PerQuantity, price.PerQuantity)
            .SetProperty(p => p.Currency, price.Currency)
            .SetProperty(p => p.EffectiveFrom, price.EffectiveFrom), cancellationToken) > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeletePriceAsync(Guid ingredientId, Guid priceId, CancellationToken cancellationToken) =>
        await db.IngredientPrices.Where(p => p.IngredientId == ingredientId && p.Id == priceId).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <inheritdoc />
    public async Task<decimal> GetStockAsync(Guid ingredientId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        return await db.StockMovements.AsNoTracking()
            .Where(m => m.IngredientId == ingredientId && (m.AvailableAt == null || m.AvailableAt <= now))
            .SumAsync(m => (decimal?)m.Quantity, cancellationToken) ?? 0m;
    }

    /// <inheritdoc />
    public async Task<PagedResult<StockMovementDto>?> ListMovementsAsync(
        Guid ownerId, Guid ingredientId, StockMovementListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!await db.Ingredients.AsNoTracking().AnyAsync(i => i.OwnerId == ownerId && i.Id == ingredientId, cancellationToken))
        {
            return null;
        }

        var movements = db.StockMovements.AsNoTracking().Where(m => m.IngredientId == ingredientId);
        var total = await movements.CountAsync(cancellationToken);
        var rows = await Project(movements.OrderByDescending(m => m.OccurredOn).ThenByDescending(m => m.CreatedAt).ThenBy(m => m.Id)
            .Skip(Paging.Skip(query.Page, query.PageSize)).Take(query.PageSize)).ToListAsync(cancellationToken);
        return new PagedResult<StockMovementDto>(rows, query.Page, query.PageSize, total);
    }

    /// <inheritdoc />
    public async Task AddRecordAsync(Guid ingredientId, IngredientRecordRequest record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var quantity = record.PackCount * record.PackSize;
        if (!record.PriceOnly)
        {
            db.StockMovements.Add(new StockMovement
            {
                IngredientId = ingredientId,
                MovementType = StockMovementType.Purchase,
                Quantity = quantity,
                OccurredOn = record.Date,
                PackCount = record.PackCount,
                PackSize = record.PackSize,
                TotalCost = record.TotalCost,
                Currency = record.Currency,
                Note = record.Note,
            });
        }

        // One price per ingredient and date: a record on a date that already has a price replaces it.
        var price = await db.IngredientPrices
            .SingleOrDefaultAsync(p => p.IngredientId == ingredientId && p.EffectiveFrom == record.Date, cancellationToken);
        if (price is null)
        {
            price = new IngredientPrice { IngredientId = ingredientId, EffectiveFrom = record.Date };
            db.IngredientPrices.Add(price);
        }

        price.Price = record.TotalCost;
        price.PerQuantity = quantity;
        price.Currency = record.Currency;
        await db.SaveChangesAsync(cancellationToken);
        if (!record.PriceOnly)
        {
            await db.Ingredients.Where(i => i.Id == ingredientId)
                .ExecuteUpdateAsync(s => s.SetProperty(i => i.TrackStock, true), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<Guid> AddAdjustmentAsync(
        Guid ingredientId, decimal quantity, DateOnly occurredOn, string note, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var movement = new StockMovement
        {
            IngredientId = ingredientId,
            MovementType = StockMovementType.Adjustment,
            Quantity = quantity,
            OccurredOn = occurredOn,
            Note = note,
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(cancellationToken);
        await db.Ingredients.Where(i => i.Id == ingredientId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.TrackStock, true), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return movement.Id;
    }

    /// <inheritdoc />
    public Task<StockMovementDto> GetMovementAsync(Guid movementId, CancellationToken cancellationToken) =>
        Project(db.StockMovements.AsNoTracking().Where(m => m.Id == movementId)).SingleAsync(cancellationToken);

    private static IQueryable<StockMovementDto> Project(IQueryable<StockMovement> movements) =>
        movements.Select(m => new StockMovementDto(m.Id, m.MovementType, m.Quantity, m.OccurredOn, m.BatchId,
            m.Batch == null ? null : m.Batch.Name, m.PackCount, m.PackSize, m.TotalCost, m.Currency, m.Note,
            new DateTimeOffset(m.CreatedAt, TimeSpan.Zero)));

    private IQueryable<IngredientDto> Project(IQueryable<Ingredient> ingredients) =>
        ingredients.Select(i => new IngredientDto(i.Id, i.Name, i.CategoryCode, i.UnitCode, i.FlourId,
            i.Flour == null ? null : i.Flour.Name, i.Flour == null ? null : i.Flour.ProteinPercent, null,
            new DateTimeOffset(i.CreatedAt, TimeSpan.Zero), new DateTimeOffset(i.UpdatedAt, TimeSpan.Zero),
            i.TrackStock, null, i.ReorderLevel, false, i.PhotoKey != null,
            db.Starters.Where(s => s.IngredientId == i.Id).Select(s => (Guid?)s.Id).FirstOrDefault()));

    /// <summary>Adds the stock of the ingredients whose stock is kept; low = below the reorder level, or below zero without one.</summary>
    private async Task<List<IngredientDto>> WithStockAsync(List<IngredientDto> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Where(r => r.TrackStock).Select(r => r.Id).ToArray();
        if (ids.Length == 0)
        {
            return rows;
        }

        // Starter that is still ripening is in the ledger with a later available_at and is not stock yet.
        var now = DateTime.UtcNow;
        var stock = await db.StockMovements.AsNoTracking()
            .Where(m => ids.Contains(m.IngredientId) && (m.AvailableAt == null || m.AvailableAt <= now))
            .GroupBy(m => m.IngredientId).Select(g => new { g.Key, Sum = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(g => g.Key, g => g.Sum, cancellationToken);
        return rows.Select(r =>
        {
            if (!r.TrackStock)
            {
                return r;
            }

            var quantity = stock.GetValueOrDefault(r.Id);
            return r with { Stock = quantity, IsLowStock = quantity < (r.ReorderLevel ?? 0m) };
        }).ToList();
    }

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
