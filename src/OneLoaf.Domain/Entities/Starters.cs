namespace OneLoaf.Domain.Entities;

/// <summary>E6, US-27, US-30. Sourdough starter.</summary>
public class Starter
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? FlourId { get; set; }
    /// <summary>
    /// The starter as an ingredient of the catalog (category starter): recipes use it and its stock is the ripe starter.
    /// Created with the starter; the name is the same in both.
    /// </summary>
    public Guid IngredientId { get; set; }
    /// <summary>Share of the fed starter that can be used for dough once it is ripe, 0-1; the rest stays for the next feeding.</summary>
    public decimal UsableShare { get; set; } = 0.667m;
    /// <summary>Ingredient whose stock a feeding takes the flour from; null = feedings do not change the stock.</summary>
    public Guid? FlourIngredientId { get; set; }
    /// <summary>Ingredient whose stock a feeding takes the water from.</summary>
    public Guid? WaterIngredientId { get; set; }
    public decimal HydrationPercent { get; set; }
    public FeedingMode FeedingMode { get; set; } = FeedingMode.Regular;
    public int FeedingIntervalHours { get; set; }
    /// <summary>Hours from feeding to peak activity (US-30).</summary>
    public decimal? RiseHours { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public Flour? Flour { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public Ingredient? FlourIngredient { get; set; }
    public Ingredient? WaterIngredient { get; set; }
    public ICollection<StarterFeeding> Feedings { get; set; } = new List<StarterFeeding>();
}

/// <summary>E6, US-28. Feeding log entry.</summary>
public class StarterFeeding
{
    public Guid Id { get; set; }
    public Guid StarterId { get; set; }
    public DateTime FedAt { get; set; }
    public decimal FlourG { get; set; }
    public decimal WaterG { get; set; }
    public decimal? StarterKeptG { get; set; }
    /// <summary>Flour ingredient this feeding used; the usual flour of the starter when not chosen.</summary>
    public Guid? FlourIngredientId { get; set; }
    /// <summary>Water ingredient this feeding used; the usual water of the starter when not chosen.</summary>
    public Guid? WaterIngredientId { get; set; }
    public decimal? TemperatureC { get; set; }
    public DateTime? NextFeedingAt { get; set; }
    public DateTime? ExpectedPeakAt { get; set; }
    public string? Note { get; set; }

    public Starter Starter { get; set; } = null!;
    public Ingredient? FlourIngredient { get; set; }
    public Ingredient? WaterIngredient { get; set; }
}

/// <summary>E6, US-29. Scheduled notification, processed by the Notification service.</summary>
public class Reminder
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public ReminderKind Kind { get; set; }
    public Guid? StarterId { get; set; }
    public Guid? BatchItemId { get; set; }
    public Guid? BatchStageId { get; set; }
    public DateTime DueAt { get; set; }
    public ReminderChannel Channel { get; set; } = ReminderChannel.Push;
    public ReminderStatus Status { get; set; } = ReminderStatus.Scheduled;
    public string Message { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public Starter? Starter { get; set; }
    public BatchItem? BatchItem { get; set; }
    public BatchStage? BatchStage { get; set; }
}
