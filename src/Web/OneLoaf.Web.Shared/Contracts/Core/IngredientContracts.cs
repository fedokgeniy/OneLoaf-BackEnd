namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>Ingredient of the caller's catalog (US-10); <see cref="CurrentPrice"/> is null until a price is entered.</summary>
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
    DateTimeOffset UpdatedAt);

/// <summary>Price valid today.</summary>
public sealed record CurrentPriceDto(decimal Price, decimal PerQuantity, decimal UnitPrice, string Currency, DateOnly EffectiveFrom);

/// <summary><c>POST/PUT /ingredients</c> body.</summary>
public sealed record IngredientRequest(string Name, string CategoryCode, string UnitCode, Guid? FlourId);

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
