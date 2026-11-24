namespace OneLoaf.Domain.Entities;

/// <summary>E5, US-20, US-26. Production batch of one or more recipes.</summary>
public class Batch
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public BatchStatus Status { get; set; } = BatchStatus.Planned;
    public BatchSource Source { get; set; } = BatchSource.Manual;
    public DateTime PlannedStartAt { get; set; }
    public DateTime? PlannedReadyFrom { get; set; }
    public DateTime? PlannedReadyTo { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>Date whose ingredient prices were used for the cost (US-15).</summary>
    public DateOnly PriceDate { get; set; }
    /// <summary>Total cost computed by the Core API when the plan was saved (snapshot).</summary>
    public decimal? TotalCost { get; set; }
    public string Currency { get; set; } = "EUR";
    /// <summary>
    /// Share of the recipe quantities that is actually used (0-1]: below 1 when the batch is baked from what is in
    /// stock. The loaves stay; the dough, the starter amount, the ingredient cost and the stock consumption shrink.
    /// </summary>
    public decimal IngredientScale { get; set; } = 1m;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public ICollection<BatchItem> Items { get; set; } = new List<BatchItem>();
}

/// <summary>E5, US-17, US-18, US-29. A recipe with a number of loaves inside a batch.</summary>
public class BatchItem
{
    public Guid Id { get; set; }
    public Guid BatchId { get; set; }
    public Guid RecipeId { get; set; }
    public Guid? StarterId { get; set; }
    public int Loaves { get; set; }
    public decimal? StarterAmountG { get; set; }
    public int? KneadingCycles { get; set; }
    /// <summary>Minutes before kneading to remind about the starter (US-29); null = no reminder.</summary>
    public int? StarterReminderMinutes { get; set; }
    public int SortOrder { get; set; }

    public Batch Batch { get; set; } = null!;
    public Recipe Recipe { get; set; } = null!;
    public Starter? Starter { get; set; }
    public ICollection<BatchStage> Stages { get; set; } = new List<BatchStage>();
}

/// <summary>E5, US-21, US-23, US-24. Stage log: planned and actual times of one stage of a batch item (KPI-1).</summary>
public class BatchStage
{
    public Guid Id { get; set; }
    public Guid BatchItemId { get; set; }
    /// <summary>Recipe stage the plan was built from; kept for reset (US-21), null if that stage was deleted.</summary>
    public Guid? RecipeStageId { get; set; }
    public int SortOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string StageTypeCode { get; set; } = string.Empty;
    /// <summary>Planned duration range of this stage in this batch; copied from the recipe, can be changed for the batch only (US-21).</summary>
    public int PlannedMinMinutes { get; set; }
    public int PlannedMaxMinutes { get; set; }
    public DateTime PlannedStartAt { get; set; }
    public DateTime PlannedEndMinAt { get; set; }
    public DateTime PlannedEndMaxAt { get; set; }
    public DateTime? ActualStartAt { get; set; }
    public DateTime? ActualEndAt { get; set; }
    public StageStatus Status { get; set; } = StageStatus.Pending;
    public int ExtendedMinutes { get; set; }
    /// <summary>True when the stage settings were changed for this batch only (US-21).</summary>
    public bool IsOverridden { get; set; }
    public decimal? ActualTemperatureC { get; set; }
    public string? Note { get; set; }

    public BatchItem BatchItem { get; set; } = null!;
    public RecipeStage? RecipeStage { get; set; }
}
