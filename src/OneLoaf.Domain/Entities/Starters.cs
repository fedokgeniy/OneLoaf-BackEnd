namespace OneLoaf.Domain.Entities;

/// <summary>E6, US-27, US-30. Sourdough starter.</summary>
public class Starter
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? FlourId { get; set; }
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
    public decimal? TemperatureC { get; set; }
    public DateTime? NextFeedingAt { get; set; }
    public DateTime? ExpectedPeakAt { get; set; }
    public string? Note { get; set; }

    public Starter Starter { get; set; } = null!;
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
