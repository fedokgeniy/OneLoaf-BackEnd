namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>
/// Ingredient of the caller's catalog (US-10); <see cref="CurrentPrice"/> is null until a price is entered.
/// <see cref="Stock"/> is in the ingredient unit and null while the stock is not kept (<see cref="TrackStock"/>).
/// </summary>
public sealed record IngredientDto(
    Guid Id,
    string Name,
    string CategoryCode,
    string UnitCode,
    Guid? FlourId,
    string? FlourName,
    decimal? ProteinPercent,
    CurrentPriceDto? CurrentPrice,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool TrackStock = false,
    decimal? Stock = null,
    decimal? ReorderLevel = null,
    bool IsLowStock = false);

/// <summary>Price valid today.</summary>
public sealed record CurrentPriceDto(decimal Price, decimal PerQuantity, decimal UnitPrice, string Currency, DateOnly EffectiveFrom);

/// <summary><c>POST/PUT /ingredients</c> body; a null <see cref="TrackStock"/> leaves the stock switch as it is.</summary>
public sealed record IngredientRequest(
    string Name, string CategoryCode, string UnitCode, Guid? FlourId, bool? TrackStock = null, decimal? ReorderLevel = null);

/// <summary><c>POST/PUT /flours</c> body (US-11).</summary>
public sealed record FlourRequest(string Name, string FlourTypeCode, decimal? ProteinPercent, string? Grind, string? Notes);

/// <summary>
/// A row of the stock ledger. <see cref="Type"/>: purchase, batch_consumption, batch_return, starter_feeding or
/// adjustment; <see cref="Quantity"/> is signed and in the ingredient unit.
/// </summary>
public sealed record StockMovementDto(
    Guid Id,
    string Type,
    decimal Quantity,
    DateOnly OccurredOn,
    Guid? BatchId,
    string? BatchName,
    int? PackCount,
    decimal? PackSize,
    decimal? TotalCost,
    string? Currency,
    string? Note,
    DateTimeOffset CreatedAt);

/// <summary><c>POST /ingredients/{id}/purchases</c> body: <see cref="PackCount"/> packs of <see cref="PackSize"/> units for <see cref="TotalCost"/>.</summary>
public sealed record PurchaseRequest(int PackCount, decimal PackSize, decimal TotalCost, string Currency, DateOnly PurchasedOn, string? Note);

/// <summary><c>POST /ingredients/{id}/stock-adjustments</c> body: the counted quantity and why it is corrected.</summary>
public sealed record StockAdjustmentRequest(decimal ActualQuantity, string Note);

/// <summary>A price of the history (US-12); <see cref="ChangePercent"/> against the previous price.</summary>
public sealed record PriceDto(
    Guid Id, decimal Price, decimal PerQuantity, decimal UnitPrice, string Currency, DateOnly EffectiveFrom, decimal? ChangePercent);

/// <summary><c>POST /ingredients/{id}/prices</c> body: <see cref="Price"/> paid for <see cref="PerQuantity"/> units.</summary>
public sealed record PriceRequest(decimal Price, decimal PerQuantity, string Currency, DateOnly EffectiveFrom);

/// <summary>Flour of the catalog (US-11).</summary>
public sealed record FlourDto(
    Guid Id, string Name, string FlourTypeCode, decimal? ProteinPercent, string? Grind, string? Notes,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>Data quality warning (<c>GET /warnings</c>), e.g. an ingredient without a price.</summary>
public sealed record WarningDto(string IssueCode, string EntityType, Guid EntityId, string Details);
