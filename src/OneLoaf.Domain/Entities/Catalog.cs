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
    /// <summary>True when the stock of the ingredient is kept: batches and feedings consume it. Switched on by the first purchase or correction.</summary>
    public bool TrackStock { get; set; }
    /// <summary>Stock level, in the ingredient unit, below which the ingredient is reported as low; null = no warning.</summary>
    public decimal? ReorderLevel { get; set; }
    /// <summary>Photo of the ingredient or its pack: key in object storage; null = no photo.</summary>
    public string? PhotoKey { get; set; }
    public string? PhotoContentType { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public Flour? Flour { get; set; }
    public ICollection<IngredientPrice> Prices { get; set; } = new List<IngredientPrice>();
}

/// <summary>
/// Stock ledger of an ingredient: purchases and corrections add, batches and starter feedings take.
/// <see cref="Quantity"/> is signed and in the ingredient unit; the stock is the sum of the rows.
/// </summary>
public class StockMovement
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public StockMovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly OccurredOn { get; set; }
    /// <summary>Batch that took or returned the quantity; NULL after the batch is deleted.</summary>
    public Guid? BatchId { get; set; }
    /// <summary>Starter feeding that took, gave back or produced the quantity.</summary>
    public Guid? FeedingId { get; set; }
    /// <summary>
    /// Moment from which the quantity counts as stock; null = at once. Ripened starter is written at the feeding and
    /// becomes available at the expected peak.
    /// </summary>
    public DateTime? AvailableAt { get; set; }
    /// <summary>Purchase: number of packs.</summary>
    public int? PackCount { get; set; }
    /// <summary>Purchase: content of one pack in the ingredient unit.</summary>
    public decimal? PackSize { get; set; }
    /// <summary>Purchase: amount paid for all packs.</summary>
    public decimal? TotalCost { get; set; }
    public string? Currency { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }

    public Ingredient Ingredient { get; set; } = null!;
    public Batch? Batch { get; set; }
    public StarterFeeding? Feeding { get; set; }
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
