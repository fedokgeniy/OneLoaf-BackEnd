using OneLoaf.Domain.Entities;

namespace OneLoaf.Infrastructure.Persistence;

/// <summary>
/// Fixed data of the reference tables. EF Core writes these rows into the migration
/// (InsertData), so the reference tables are filled by the same script that creates them.
/// Changing a row here and adding a migration produces the UPDATE/INSERT/DELETE for it.
/// </summary>
public static class ReferenceData
{
    public static readonly Unit[] Units =
    {
        new() { Code = "g",   Name = "gram",       Dimension = "mass",   FactorToBase = 1m,    SortOrder = 1 },
        new() { Code = "kg",  Name = "kilogram",   Dimension = "mass",   FactorToBase = 1000m, SortOrder = 2 },
        new() { Code = "ml",  Name = "millilitre", Dimension = "volume", FactorToBase = 1m,    SortOrder = 3 },
        new() { Code = "l",   Name = "litre",      Dimension = "volume", FactorToBase = 1000m, SortOrder = 4 },
        new() { Code = "pcs", Name = "piece",      Dimension = "count",  FactorToBase = 1m,    SortOrder = 5 },
    };

    public static readonly Currency[] Currencies =
    {
        new() { Code = "EUR", Name = "Euro",           MinorUnits = 2 },
        new() { Code = "USD", Name = "US dollar",      MinorUnits = 2 },
        new() { Code = "GBP", Name = "Pound sterling", MinorUnits = 2 },
        new() { Code = "PLN", Name = "Polish zloty",   MinorUnits = 2 },
    };

    public static readonly StageType[] StageTypes =
    {
        new() { Code = "autolyse",          Name = "Autolyse",          IsFermentation = false, DefaultUsesOven = false, DefaultUsesMixer = false, SortOrder = 1 },
        new() { Code = "mix",               Name = "Mixing",            IsFermentation = false, DefaultUsesOven = false, DefaultUsesMixer = true,  SortOrder = 2 },
        new() { Code = "bulk_fermentation", Name = "Bulk fermentation", IsFermentation = true,  DefaultUsesOven = false, DefaultUsesMixer = false, SortOrder = 3 },
        new() { Code = "fold",              Name = "Stretch and fold",  IsFermentation = true,  DefaultUsesOven = false, DefaultUsesMixer = false, SortOrder = 4 },
        new() { Code = "preshape",          Name = "Pre-shaping",       IsFermentation = false, DefaultUsesOven = false, DefaultUsesMixer = false, SortOrder = 5 },
        new() { Code = "shape",             Name = "Shaping",           IsFermentation = false, DefaultUsesOven = false, DefaultUsesMixer = false, SortOrder = 6 },
        new() { Code = "proof",             Name = "Proofing",          IsFermentation = true,  DefaultUsesOven = false, DefaultUsesMixer = false, SortOrder = 7 },
        new() { Code = "bake",              Name = "Baking",            IsFermentation = false, DefaultUsesOven = true,  DefaultUsesMixer = false, SortOrder = 8 },
        new() { Code = "cool",              Name = "Cooling",           IsFermentation = false, DefaultUsesOven = false, DefaultUsesMixer = false, SortOrder = 9 },
        new() { Code = "other",             Name = "Other",             IsFermentation = false, DefaultUsesOven = false, DefaultUsesMixer = false, SortOrder = 10 },
    };

    public static readonly IngredientCategory[] IngredientCategories =
    {
        new() { Code = "flour",     Name = "Flour",          IsFlour = true,  IsLiquid = false, SortOrder = 1 },
        new() { Code = "water",     Name = "Water",          IsFlour = false, IsLiquid = true,  SortOrder = 2 },
        new() { Code = "salt",      Name = "Salt",           IsFlour = false, IsLiquid = false, SortOrder = 3 },
        new() { Code = "starter",   Name = "Starter",        IsFlour = false, IsLiquid = false, SortOrder = 4 },
        new() { Code = "yeast",     Name = "Yeast",          IsFlour = false, IsLiquid = false, SortOrder = 5 },
        new() { Code = "sweetener", Name = "Sweetener",      IsFlour = false, IsLiquid = false, SortOrder = 6 },
        new() { Code = "fat",       Name = "Fat",            IsFlour = false, IsLiquid = false, SortOrder = 7 },
        new() { Code = "dairy",     Name = "Dairy",          IsFlour = false, IsLiquid = true,  SortOrder = 8 },
        new() { Code = "add_in",    Name = "Add-in (seeds, nuts, fruit)", IsFlour = false, IsLiquid = false, SortOrder = 9 },
        new() { Code = "packaging", Name = "Packaging",      IsFlour = false, IsLiquid = false, SortOrder = 10 },
        new() { Code = "other",     Name = "Other",          IsFlour = false, IsLiquid = false, SortOrder = 11 },
    };

    public static readonly FlourType[] FlourTypes =
    {
        new() { Code = "wheat",  Name = "Wheat",               SortOrder = 1 },
        new() { Code = "rye",    Name = "Rye",                 SortOrder = 2 },
        new() { Code = "spelt",  Name = "Spelt",               SortOrder = 3 },
        new() { Code = "whole",  Name = "Whole grain (mixed)", SortOrder = 4 },
        new() { Code = "barley", Name = "Barley",              SortOrder = 5 },
        new() { Code = "oat",    Name = "Oat",                 SortOrder = 6 },
        new() { Code = "corn",   Name = "Corn",                SortOrder = 7 },
        new() { Code = "other",  Name = "Other",               SortOrder = 8 },
    };
}
