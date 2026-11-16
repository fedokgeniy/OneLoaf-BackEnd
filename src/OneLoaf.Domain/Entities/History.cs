namespace OneLoaf.Domain.Entities;

/// <summary>E7, US-31, US-32, US-36. Created automatically when a batch is done.</summary>
public class BakeRecord
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? BatchId { get; set; }
    public DateOnly BakedOn { get; set; }
    public int TotalLoaves { get; set; }
    /// <summary>Cost on the bake date (snapshot, not recalculated later).</summary>
    public decimal? TotalCost { get; set; }
    public string Currency { get; set; } = "EUR";
    public short? Rating { get; set; }
    public string? Comment { get; set; }
    public string? AssistantAnalysis { get; set; }
    public DateTime? AnalysisAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public Batch? Batch { get; set; }
    public ICollection<BakeRecordItem> Items { get; set; } = new List<BakeRecordItem>();
    public ICollection<BakeRecordPhoto> Photos { get; set; } = new List<BakeRecordPhoto>();
}

/// <summary>E7, US-31. Snapshot of what was baked; survives later edits or deletion of the recipe.</summary>
public class BakeRecordItem
{
    public Guid Id { get; set; }
    public Guid BakeRecordId { get; set; }
    public Guid? RecipeId { get; set; }
    public string RecipeTitle { get; set; } = string.Empty;
    public int Loaves { get; set; }
    public decimal? Cost { get; set; }

    public BakeRecord BakeRecord { get; set; } = null!;
    public Recipe? Recipe { get; set; }
}

/// <summary>E7, US-51. Crumb photo; the file itself is in object storage, the DB keeps the key.</summary>
public class BakeRecordPhoto
{
    public Guid Id { get; set; }
    public Guid BakeRecordId { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public BakeRecord BakeRecord { get; set; } = null!;
}
