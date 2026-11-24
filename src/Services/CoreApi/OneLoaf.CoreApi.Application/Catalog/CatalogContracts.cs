using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.Catalog;

/// <summary>Flour of the user's catalog (US-11).</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name, unique within the user.</param>
/// <param name="FlourTypeCode">Grain type code.</param>
/// <param name="ProteinPercent">Protein, %.</param>
/// <param name="Grind">Grind.</param>
/// <param name="Notes">Notes.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="UpdatedAt">Last change.</param>
public sealed record FlourDto(
    Guid Id, string Name, string FlourTypeCode, decimal? ProteinPercent, string? Grind, string? Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>Body of <c>POST/PUT /flours</c>.</summary>
/// <param name="Name">Name.</param>
/// <param name="FlourTypeCode">Grain type code.</param>
/// <param name="ProteinPercent">Protein, 0-100 %.</param>
/// <param name="Grind">Grind.</param>
/// <param name="Notes">Notes.</param>
public sealed record FlourRequest(string Name, string FlourTypeCode, decimal? ProteinPercent, string? Grind, string? Notes);

/// <summary>Query of <c>GET /flours</c>.</summary>
/// <param name="Search">Part of the name.</param>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record FlourListQuery(string? Search = null, int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>Price valid today.</summary>
/// <param name="Price">Price for <paramref name="PerQuantity"/> units.</param>
/// <param name="PerQuantity">Quantity in the ingredient unit the price applies to.</param>
/// <param name="UnitPrice">Price of one unit.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="EffectiveFrom">Date from which the price applies.</param>
public sealed record CurrentPriceDto(decimal Price, decimal PerQuantity, decimal UnitPrice, string Currency, DateOnly EffectiveFrom);

/// <summary>Ingredient of the user's catalog (US-10).</summary>
/// <param name="Id">Id.</param>
/// <param name="Name">Name, unique within the user.</param>
/// <param name="CategoryCode">Category code.</param>
/// <param name="UnitCode">Unit of recipe quantities and prices.</param>
/// <param name="FlourId">Linked flour, if any.</param>
/// <param name="FlourName">Name of the linked flour.</param>
/// <param name="ProteinPercent">Protein of the linked flour.</param>
/// <param name="CurrentPrice">Price valid today; null = no price yet.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="UpdatedAt">Last change.</param>
/// <param name="TrackStock">True when the stock is kept: batches and starter feedings take from it.</param>
/// <param name="Stock">Stock in the ingredient unit; null when the stock is not kept.</param>
/// <param name="ReorderLevel">Stock level below which the ingredient is low.</param>
/// <param name="IsLowStock">True when the stock is below the reorder level, or below zero without a level.</param>
public sealed record IngredientDto(
    Guid Id, string Name, string CategoryCode, string UnitCode, Guid? FlourId, string? FlourName, decimal? ProteinPercent,
    CurrentPriceDto? CurrentPrice, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    bool TrackStock = false, decimal? Stock = null, decimal? ReorderLevel = null, bool IsLowStock = false);

/// <summary>Body of <c>POST/PUT /ingredients</c>.</summary>
/// <param name="Name">Name.</param>
/// <param name="CategoryCode">Category code.</param>
/// <param name="UnitCode">Unit code.</param>
/// <param name="FlourId">Flour of the user's catalog.</param>
/// <param name="TrackStock">Keep the stock of the ingredient; null leaves the switch as it is (off for a new ingredient).</param>
/// <param name="ReorderLevel">Stock level below which the ingredient is reported as low; null = no level.</param>
public sealed record IngredientRequest(
    string Name, string CategoryCode, string UnitCode, Guid? FlourId, bool? TrackStock = null, decimal? ReorderLevel = null);

/// <summary>Query of <c>GET /ingredients</c>.</summary>
/// <param name="Search">Part of the name.</param>
/// <param name="Category">Category code.</param>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record IngredientListQuery(
    string? Search = null, string? Category = null, int Page = 1, int PageSize = Paging.DefaultPageSize);

/// <summary>A row of the price history (US-12).</summary>
/// <param name="Id">Id.</param>
/// <param name="Price">Price for <paramref name="PerQuantity"/> units.</param>
/// <param name="PerQuantity">Quantity the price applies to.</param>
/// <param name="UnitPrice">Price of one unit.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="EffectiveFrom">Date from which the price applies.</param>
/// <param name="ChangePercent">Change of the unit price against the previous price in the same currency, %.</param>
public sealed record PriceDto(
    Guid Id, decimal Price, decimal PerQuantity, decimal UnitPrice, string Currency, DateOnly EffectiveFrom,
    decimal? ChangePercent);

/// <summary>Body of <c>POST /ingredients/{id}/prices</c>.</summary>
/// <param name="Price">Price, at least 0.</param>
/// <param name="PerQuantity">Quantity the price applies to, more than 0.</param>
/// <param name="Currency">Currency code.</param>
/// <param name="EffectiveFrom">Date from which the price applies.</param>
public sealed record PriceRequest(decimal Price, decimal PerQuantity, string Currency, DateOnly EffectiveFrom);

/// <summary>A stored price before the change is computed.</summary>
/// <param name="Id">Id.</param>
/// <param name="Price">Price.</param>
/// <param name="PerQuantity">Quantity.</param>
/// <param name="Currency">Currency.</param>
/// <param name="EffectiveFrom">Effective date.</param>
public sealed record PriceRow(Guid Id, decimal Price, decimal PerQuantity, string Currency, DateOnly EffectiveFrom);

/// <summary>Data access for <c>flours</c>; every method is scoped to the owner.</summary>
public interface IFlourRepository
{
    /// <summary>Page of flours ordered by name.</summary>
    Task<PagedResult<FlourDto>> ListAsync(Guid ownerId, FlourListQuery query, CancellationToken cancellationToken);

    /// <summary>The flour, or null when it does not exist or belongs to another user.</summary>
    Task<FlourDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when the name is taken by another flour of the user.</summary>
    Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Creates a flour; returns its id.</summary>
    Task<Guid> CreateAsync(Guid ownerId, FlourRequest flour, CancellationToken cancellationToken);

    /// <summary>Replaces a flour; false when not found for the owner.</summary>
    Task<bool> UpdateAsync(Guid ownerId, Guid id, FlourRequest flour, CancellationToken cancellationToken);

    /// <summary>Deletes a flour; false when not found for the owner.</summary>
    Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
}

/// <summary>Data access for <c>ingredients</c> and <c>ingredient_prices</c>; scoped to the owner.</summary>
public interface IIngredientRepository
{
    /// <summary>Page of ingredients with today's price, ordered by name.</summary>
    Task<PagedResult<IngredientDto>> ListAsync(Guid ownerId, IngredientListQuery query, CancellationToken cancellationToken);

    /// <summary>The ingredient, or null.</summary>
    Task<IngredientDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when the name is taken by another ingredient of the user.</summary>
    Task<bool> NameExistsAsync(Guid ownerId, string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Ids from <paramref name="ids"/> that are ingredients of the owner.</summary>
    Task<IReadOnlySet<Guid>> GetOwnedIdsAsync(Guid ownerId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Ids of the owner's ingredients with one of the names, compared without case.</summary>
    Task<IReadOnlyDictionary<string, Guid>> FindByNamesAsync(Guid ownerId, IReadOnlyCollection<string> names, CancellationToken cancellationToken);

    /// <summary>True when the ingredient is used in a recipe line.</summary>
    Task<bool> IsUsedAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Creates an ingredient; returns its id.</summary>
    Task<Guid> CreateAsync(Guid ownerId, IngredientRequest ingredient, CancellationToken cancellationToken);

    /// <summary>Replaces an ingredient; false when not found for the owner.</summary>
    Task<bool> UpdateAsync(Guid ownerId, Guid id, IngredientRequest ingredient, CancellationToken cancellationToken);

    /// <summary>Deletes an ingredient; false when not found for the owner.</summary>
    Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Price history of an owned ingredient, newest first; null when the ingredient is not found.</summary>
    Task<IReadOnlyList<PriceRow>?> GetPricesAsync(Guid ownerId, Guid ingredientId, CancellationToken cancellationToken);

    /// <summary>Adds a price; returns its id.</summary>
    Task<Guid> AddPriceAsync(Guid ingredientId, PriceRequest price, CancellationToken cancellationToken);

    /// <summary>Replaces a price of the ingredient; false when the price is not found.</summary>
    Task<bool> UpdatePriceAsync(Guid ingredientId, Guid priceId, PriceRequest price, CancellationToken cancellationToken);

    /// <summary>Deletes a price of the ingredient; false when the price is not found.</summary>
    Task<bool> DeletePriceAsync(Guid ingredientId, Guid priceId, CancellationToken cancellationToken);

    /// <summary>Stock of the ingredient in its unit (<c>fn_stock_balance</c>).</summary>
    Task<decimal> GetStockAsync(Guid ingredientId, CancellationToken cancellationToken);

    /// <summary>Page of the stock ledger of an owned ingredient, newest first; null when the ingredient is not found.</summary>
    Task<PagedResult<StockMovementDto>?> ListMovementsAsync(
        Guid ownerId, Guid ingredientId, StockMovementListQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a purchase in one transaction: the stock movement, the price of the purchase date (created or replaced)
    /// and the stock switch of the ingredient. Returns the id of the movement.
    /// </summary>
    Task<Guid> AddPurchaseAsync(Guid ingredientId, PurchaseRequest purchase, CancellationToken cancellationToken);

    /// <summary>Stores a correction of the stock and switches the stock of the ingredient on; returns the id of the movement.</summary>
    Task<Guid> AddAdjustmentAsync(Guid ingredientId, decimal quantity, DateOnly occurredOn, string note, CancellationToken cancellationToken);

    /// <summary>One movement of the ledger.</summary>
    Task<StockMovementDto> GetMovementAsync(Guid movementId, CancellationToken cancellationToken);
}
