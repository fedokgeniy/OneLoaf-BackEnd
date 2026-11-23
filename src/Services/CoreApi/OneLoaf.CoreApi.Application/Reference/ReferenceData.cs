using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.Reference;

/// <summary>Unit of measure.</summary>
/// <param name="Code">Code, for example <c>g</c>.</param>
/// <param name="Name">Name.</param>
/// <param name="Dimension">mass, volume or count.</param>
/// <param name="FactorToBase">Base units (g, ml, pcs) in one unit.</param>
public sealed record UnitDto(string Code, string Name, string Dimension, decimal FactorToBase);

/// <summary>Currency.</summary>
/// <param name="Code">ISO 4217 code.</param>
/// <param name="Name">Name.</param>
/// <param name="MinorUnits">Digits after the decimal point.</param>
public sealed record CurrencyDto(string Code, string Name, int MinorUnits);

/// <summary>Stage type with the defaults used for new stages.</summary>
/// <param name="Code">Code.</param>
/// <param name="Name">Name.</param>
/// <param name="IsFermentation">Fermentation stage (proofing mode allowed).</param>
/// <param name="DefaultUsesOven">Default of <c>usesOven</c>.</param>
/// <param name="DefaultUsesMixer">Default of <c>usesMixer</c>.</param>
public sealed record StageTypeDto(string Code, string Name, bool IsFermentation, bool DefaultUsesOven, bool DefaultUsesMixer);

/// <summary>Ingredient category.</summary>
/// <param name="Code">Code.</param>
/// <param name="Name">Name.</param>
/// <param name="IsFlour">Counted as flour.</param>
/// <param name="IsLiquid">Counted as liquid.</param>
public sealed record IngredientCategoryDto(string Code, string Name, bool IsFlour, bool IsLiquid);

/// <summary>Flour (grain) type.</summary>
/// <param name="Code">Code.</param>
/// <param name="Name">Name.</param>
public sealed record FlourTypeDto(string Code, string Name);

/// <summary>Read access to the reference tables (fixed data shipped with the schema).</summary>
public interface IReferenceDataReader
{
    /// <summary>Units ordered for display.</summary>
    Task<IReadOnlyList<UnitDto>> GetUnitsAsync(CancellationToken cancellationToken);

    /// <summary>Currencies.</summary>
    Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken);

    /// <summary>Stage types ordered for display.</summary>
    Task<IReadOnlyList<StageTypeDto>> GetStageTypesAsync(CancellationToken cancellationToken);

    /// <summary>Ingredient categories ordered for display.</summary>
    Task<IReadOnlyList<IngredientCategoryDto>> GetIngredientCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>Flour types ordered for display.</summary>
    Task<IReadOnlyList<FlourTypeDto>> GetFlourTypesAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Checks codes in requests against the reference tables, so an unknown code is a 400 with a field error instead of a
/// foreign key violation.
/// </summary>
public sealed class ReferenceGuard(IReferenceDataReader reader)
{
    /// <summary>Throws a validation error when the currency code is unknown.</summary>
    public async Task EnsureCurrencyAsync(string code, string propertyName, CancellationToken cancellationToken)
    {
        var all = await reader.GetCurrenciesAsync(cancellationToken);
        Ensure(all.Any(c => c.Code == code), propertyName, "currency", code);
    }

    /// <summary>Throws a validation error when the unit code is unknown.</summary>
    public async Task EnsureUnitAsync(string code, string propertyName, CancellationToken cancellationToken)
    {
        var all = await reader.GetUnitsAsync(cancellationToken);
        Ensure(all.Any(c => c.Code == code), propertyName, "unit", code);
    }

    /// <summary>Throws a validation error when the ingredient category code is unknown.</summary>
    public async Task EnsureIngredientCategoryAsync(string code, string propertyName, CancellationToken cancellationToken)
    {
        var all = await reader.GetIngredientCategoriesAsync(cancellationToken);
        Ensure(all.Any(c => c.Code == code), propertyName, "ingredient category", code);
    }

    /// <summary>Throws a validation error when the flour type code is unknown.</summary>
    public async Task EnsureFlourTypeAsync(string code, string propertyName, CancellationToken cancellationToken)
    {
        var all = await reader.GetFlourTypesAsync(cancellationToken);
        Ensure(all.Any(c => c.Code == code), propertyName, "flour type", code);
    }

    /// <summary>Returns the stage types by code (for defaults and checks of a whole recipe).</summary>
    public async Task<IReadOnlyDictionary<string, StageTypeDto>> GetStageTypesAsync(CancellationToken cancellationToken) =>
        (await reader.GetStageTypesAsync(cancellationToken)).ToDictionary(s => s.Code, StringComparer.Ordinal);

    private static void Ensure(bool exists, string propertyName, string kind, string code)
    {
        if (!exists)
        {
            throw ValidationFailures.For(propertyName, $"Unknown {kind} '{code}'.");
        }
    }
}
