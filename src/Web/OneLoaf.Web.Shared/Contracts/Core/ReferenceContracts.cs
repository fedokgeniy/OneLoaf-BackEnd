namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>Unit of measure (<c>GET /reference/units</c>).</summary>
public sealed record UnitDto(string Code, string Name, string Dimension, decimal FactorToBase);

/// <summary>Stage type (<c>GET /reference/stage-types</c>).</summary>
public sealed record StageTypeDto(string Code, string Name, bool IsFermentation, bool DefaultUsesOven, bool DefaultUsesMixer);

/// <summary>Currency (<c>GET /reference/currencies</c>).</summary>
public sealed record CurrencyDto(string Code, string Name, int MinorUnits);

/// <summary>Ingredient category (<c>GET /reference/ingredient-categories</c>).</summary>
public sealed record IngredientCategoryDto(string Code, string Name, bool IsFlour, bool IsLiquid);

/// <summary>Flour type (<c>GET /reference/flour-types</c>).</summary>
public sealed record FlourTypeDto(string Code, string Name);
