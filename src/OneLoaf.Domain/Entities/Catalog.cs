namespace OneLoaf.Domain.Entities;

/// <summary>E3, US-11. Flour characteristics in the user's catalog.</summary>
public class Flour
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Code from flour_types.</summary>
    public string FlourTypeCode { get; set; } = string.Empty;
    public decimal? ProteinPercent { get; set; }
    public string? Grind { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
}

/// <summary>E3, US-10. Ingredient with a unit of measure.</summary>
public class Ingredient
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Code from ingredient_categories.</summary>
    public string CategoryCode { get; set; } = string.Empty;
    /// <summary>Code from units; recipe quantities and prices of this ingredient use this unit.</summary>
    public string UnitCode { get; set; } = string.Empty;
    public Guid? FlourId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public Flour? Flour { get; set; }
    public ICollection<IngredientPrice> Prices { get; set; } = new List<IngredientPrice>();
}

/// <summary>
/// E3, US-12, US-15. Price history: a new row per change, old rows are kept.
/// The price applies to <see cref="PerQuantity"/> units of the ingredient (e.g. 1.35 per 1000 g).
/// The price valid on date D is the row with the latest EffectiveFrom &lt;= D.
/// </summary>
public class IngredientPrice
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public decimal Price { get; set; }
    public decimal PerQuantity { get; set; }
    public string Currency { get; set; } = "EUR";
    public DateOnly EffectiveFrom { get; set; }
    public DateTime CreatedAt { get; set; }

    public Ingredient Ingredient { get; set; } = null!;
}
