namespace OneLoaf.Domain.Entities;

// Reference (lookup) tables. Their rows are fixed data shipped with the schema:
// they are inserted by the InitialSchema migration (HasData in ReferenceData.cs) and
// changed only by new migrations. Users and the application can read them, not modify them.

/// <summary>Unit of measure for ingredients. Quantities are converted to the base unit of a dimension with FactorToBase.</summary>
public class Unit
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>mass, volume or count.</summary>
    public string Dimension { get; set; } = string.Empty;
    /// <summary>How many base units (g, ml, pcs) one unit contains: kg = 1000.</summary>
    public decimal FactorToBase { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>ISO 4217 currency.</summary>
public class Currency
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>Number of digits after the decimal point used for display and rounding.</summary>
    public short MinorUnits { get; set; }
}

/// <summary>Type of a recipe / batch stage with defaults used when a stage is created.</summary>
public class StageType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFermentation { get; set; }
    public bool DefaultUsesOven { get; set; }
    public bool DefaultUsesMixer { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Category of an ingredient; flags are used for baker's percentages and hydration.</summary>
public class IngredientCategory
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsFlour { get; set; }
    public bool IsLiquid { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Type (grain) of flour.</summary>
public class FlourType
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
