using System.Text.Json.Serialization;

namespace OneLoaf.Messaging.Contracts;

/// <summary>Base of every event published to the <c>oneloaf.events</c> exchange.</summary>
/// <param name="OccurredAt">When the change happened.</param>
public abstract record IntegrationEvent(DateTimeOffset OccurredAt)
{
    /// <summary>Routing key of the event (one of <see cref="RoutingKeys"/>); not part of the message body.</summary>
    [JsonIgnore]
    public abstract string RoutingKey { get; }
}

/// <summary>A batch was planned (manual form or confirmed assistant proposal).</summary>
/// <param name="BatchId">Batch.</param>
/// <param name="OwnerId">Owner.</param>
/// <param name="Name">Batch name.</param>
/// <param name="PlannedStartAt">Planned start.</param>
/// <param name="PlannedReadyFrom">Start of the ready window.</param>
/// <param name="PlannedReadyTo">End of the ready window.</param>
/// <param name="FirstReminderAt">First scheduled reminder of the batch, if any.</param>
/// <param name="OccurredAt">When the batch was saved.</param>
public sealed record BatchPlanned(
    Guid BatchId, Guid OwnerId, string Name, DateTimeOffset PlannedStartAt, DateTimeOffset? PlannedReadyFrom,
    DateTimeOffset? PlannedReadyTo, DateTimeOffset? FirstReminderAt, DateTimeOffset OccurredAt) : IntegrationEvent(OccurredAt)
{
    /// <inheritdoc />
    public override string RoutingKey => RoutingKeys.BatchPlanned;
}

/// <summary>A batch was completed and its history record created.</summary>
/// <param name="BatchId">Batch.</param>
/// <param name="OwnerId">Owner.</param>
/// <param name="BakeRecordId">History record created by the database.</param>
/// <param name="OccurredAt">Completion time.</param>
public sealed record BatchCompleted(Guid BatchId, Guid OwnerId, Guid? BakeRecordId, DateTimeOffset OccurredAt)
    : IntegrationEvent(OccurredAt)
{
    /// <inheritdoc />
    public override string RoutingKey => RoutingKeys.BatchCompleted;
}

/// <summary>A starter was fed; the database computed the next feeding and the peak.</summary>
/// <param name="StarterId">Starter.</param>
/// <param name="OwnerId">Owner.</param>
/// <param name="FedAt">Feeding time.</param>
/// <param name="NextFeedingAt">Next feeding.</param>
/// <param name="ExpectedPeakAt">Expected peak activity.</param>
/// <param name="OccurredAt">When the feeding was recorded.</param>
public sealed record StarterFed(
    Guid StarterId, Guid OwnerId, DateTimeOffset FedAt, DateTimeOffset? NextFeedingAt, DateTimeOffset? ExpectedPeakAt,
    DateTimeOffset OccurredAt) : IntegrationEvent(OccurredAt)
{
    /// <inheritdoc />
    public override string RoutingKey => RoutingKeys.StarterFed;
}

/// <summary>An account and its workspace were deleted (data removed by cascade).</summary>
/// <param name="UserId">Deleted user.</param>
/// <param name="OccurredAt">Deletion time.</param>
public sealed record AccountDeleted(Guid UserId, DateTimeOffset OccurredAt) : IntegrationEvent(OccurredAt)
{
    /// <inheritdoc />
    public override string RoutingKey => RoutingKeys.AccountDeleted;
}

/// <summary>A reminder is due and should be delivered (published by the Notification scheduler).</summary>
/// <param name="ReminderId">Reminder row.</param>
/// <param name="OwnerId">Owner.</param>
/// <param name="DueAt">Due time.</param>
/// <param name="OccurredAt">When the scheduler picked it up.</param>
public sealed record ReminderDue(Guid ReminderId, Guid OwnerId, DateTimeOffset DueAt, DateTimeOffset OccurredAt)
    : IntegrationEvent(OccurredAt)
{
    /// <inheritdoc />
    public override string RoutingKey => RoutingKeys.ReminderDue;
}
