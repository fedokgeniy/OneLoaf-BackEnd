namespace OneLoaf.Web.Shared.State;

/// <summary>
/// Global action: a batch was saved — from the new batch form or by confirming an assistant plan. Production reloads its
/// lists, Home reloads the active batch; raised in Shared so features stay independent (architecture.md §2, rule 1).
/// </summary>
public sealed record BatchCreated(Guid BatchId, string Name);

/// <summary>Global action: a batch was completed and became a bake record (US-31); History reloads.</summary>
public sealed record BatchCompleted(Guid BatchId, Guid? BakeRecordId);
