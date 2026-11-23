using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.Application.Batches;

/// <summary>Item of a batch request: a recipe, a number of loaves and the starter to use.</summary>
/// <param name="RecipeId">Recipe or variant of the user.</param>
/// <param name="Loaves">Number of loaves, 1-1000.</param>
/// <param name="StarterId">Starter of the user.</param>
/// <param name="StarterReminderMinutes">Minutes before kneading to remind about the starter; null = no reminder.</param>
public sealed record BatchItemRequest(Guid RecipeId, int Loaves, Guid? StarterId = null, int? StarterReminderMinutes = null);

/// <summary>Body of <c>POST /batches</c>, <c>POST /batches/preview</c> and <c>PUT /batches/{id}</c> (US-20).</summary>
/// <param name="Name">Batch name.</param>
/// <param name="PlannedStartAt">Planned start, ISO 8601 with offset.</param>
/// <param name="Items">Items, at least one.</param>
/// <param name="PriceDate">Date of the prices for the planned cost; defaults to today in the kitchen time zone.</param>
/// <param name="Notes">Notes.</param>
/// <param name="Source">manual (form) or assistant (confirmed proposal, US-34).</param>
public sealed record BatchRequest(
    string Name, DateTimeOffset PlannedStartAt, IReadOnlyList<BatchItemRequest> Items, DateOnly? PriceDate = null,
    string? Notes = null, BatchSource Source = BatchSource.Manual);

/// <summary>Planned and actual data of a batch stage.</summary>
/// <param name="Id">Stage id.</param>
/// <param name="SortOrder">Order.</param>
/// <param name="Name">Name at planning time.</param>
/// <param name="StageTypeCode">Stage type.</param>
/// <param name="PlannedMinMinutes">Planned minimum duration (may be changed for this batch).</param>
/// <param name="PlannedMaxMinutes">Planned maximum duration.</param>
/// <param name="PlannedStartAt">Planned start.</param>
/// <param name="PlannedEndMinAt">Planned earliest end.</param>
/// <param name="PlannedEndMaxAt">Planned latest end.</param>
/// <param name="ActualStartAt">Actual start.</param>
/// <param name="ActualEndAt">Actual end.</param>
/// <param name="Status">pending, running, done, skipped.</param>
/// <param name="ExtendedMinutes">Minutes added with "+30 min".</param>
/// <param name="IsOverridden">Durations changed for this batch only.</param>
/// <param name="ActualTemperatureC">Measured temperature.</param>
/// <param name="Note">Note of the baker.</param>
public sealed record BatchStageDto(
    Guid Id, int SortOrder, string Name, string StageTypeCode, int PlannedMinMinutes, int PlannedMaxMinutes,
    DateTimeOffset PlannedStartAt, DateTimeOffset PlannedEndMinAt, DateTimeOffset PlannedEndMaxAt,
    DateTimeOffset? ActualStartAt, DateTimeOffset? ActualEndAt, StageStatus Status, int ExtendedMinutes, bool IsOverridden,
    decimal? ActualTemperatureC, string? Note);

/// <summary>Item of a batch with the figures computed by the plan.</summary>
/// <param name="Id">Item id.</param>
/// <param name="RecipeId">Recipe.</param>
/// <param name="RecipeTitle">Recipe title.</param>
/// <param name="Loaves">Loaves.</param>
/// <param name="StarterId">Starter.</param>
/// <param name="StarterAmountG">Starter to prepare, g.</param>
/// <param name="KneadingCycles">Kneading cycles for the bowl; null when no bowl is set.</param>
/// <param name="StarterReminderMinutes">Starter reminder before kneading, minutes.</param>
/// <param name="SortOrder">Order.</param>
/// <param name="Stages">Stages in order.</param>
public sealed record BatchItemDto(
    Guid Id, Guid RecipeId, string RecipeTitle, int Loaves, Guid? StarterId, decimal? StarterAmountG, int? KneadingCycles,
    int? StarterReminderMinutes, int SortOrder, IReadOnlyList<BatchStageDto> Stages);

/// <summary>A production batch with its plan (US-20) and progress (US-23).</summary>
/// <param name="Id">Batch id (empty for a preview).</param>
/// <param name="Name">Name.</param>
/// <param name="Status">planned, in_progress, done, cancelled.</param>
/// <param name="Source">manual or assistant.</param>
/// <param name="PlannedStartAt">Planned start.</param>
/// <param name="PlannedReadyFrom">Start of the ready window.</param>
/// <param name="PlannedReadyTo">End of the ready window.</param>
/// <param name="ActualStartAt">Actual start.</param>
/// <param name="CompletedAt">Completion time.</param>
/// <param name="PriceDate">Price date of the planned cost.</param>
/// <param name="TotalCost">Planned cost; null when prices are missing.</param>
/// <param name="Currency">Currency.</param>
/// <param name="Notes">Notes.</param>
/// <param name="Items">Items with stages.</param>
/// <param name="BakeRecordId">History record of a completed batch.</param>
public sealed record BatchDto(
    Guid Id, string Name, BatchStatus Status, BatchSource Source, DateTimeOffset PlannedStartAt,
    DateTimeOffset? PlannedReadyFrom, DateTimeOffset? PlannedReadyTo, DateTimeOffset? ActualStartAt,
    DateTimeOffset? CompletedAt, DateOnly PriceDate, decimal? TotalCost, string Currency, string? Notes,
    IReadOnlyList<BatchItemDto> Items, Guid? BakeRecordId);

/// <summary>Batch in the calendar (US-26).</summary>
/// <param name="Id">Batch id.</param>
/// <param name="Name">Name.</param>
/// <param name="Status">Status.</param>
/// <param name="PlannedStartAt">Planned start.</param>
/// <param name="PlannedReadyFrom">Start of the ready window.</param>
/// <param name="PlannedReadyTo">End of the ready window.</param>
/// <param name="TotalLoaves">Loaves over all items.</param>
public sealed record BatchSummaryDto(
    Guid Id, string Name, BatchStatus Status, DateTimeOffset PlannedStartAt, DateTimeOffset? PlannedReadyFrom,
    DateTimeOffset? PlannedReadyTo, int TotalLoaves);

/// <summary>Query of <c>GET /batches</c> (calendar).</summary>
/// <param name="From">Planned start from (inclusive).</param>
/// <param name="To">Planned start to (exclusive).</param>
/// <param name="Status">Only this status: planned, in_progress, done or cancelled.</param>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record BatchListQuery(
    DateTimeOffset? From = null, DateTimeOffset? To = null, string? Status = null, int Page = 1,
    int PageSize = Paging.DefaultPageSize);

/// <summary>Body of the stage actions start/complete/skip (US-23).</summary>
/// <param name="ActualTemperatureC">Measured temperature.</param>
/// <param name="Note">Note.</param>
public sealed record StageActionRequest(decimal? ActualTemperatureC = null, string? Note = null);

/// <summary>Body of <c>POST .../extend</c> (US-24).</summary>
/// <param name="Minutes">Minutes to add, 1-1440.</param>
public sealed record ExtendStageRequest(int Minutes = 30);

/// <summary>Body of <c>PATCH .../stages/{stageId}</c> (US-21): durations for this batch only.</summary>
/// <param name="PlannedMinMinutes">Minimum duration.</param>
/// <param name="PlannedMaxMinutes">Maximum duration.</param>
public sealed record StageDurationRequest(int PlannedMinMinutes, int PlannedMaxMinutes);

/// <summary>Result of <c>POST /batches/{id}/complete</c> (US-31).</summary>
/// <param name="BakeRecordId">History record created by the database.</param>
public sealed record CompleteBatchResponse(Guid? BakeRecordId);

/// <summary>Kneads of the same dough merged across items (US-22).</summary>
/// <param name="RecipeId">Recipe (the dough).</param>
/// <param name="RecipeTitle">Recipe title.</param>
/// <param name="ItemIds">Items of the batch made from this dough.</param>
/// <param name="Loaves">Loaves over these items.</param>
/// <param name="DoughWeightG">Dough weight of all loaves, g.</param>
/// <param name="SeparateCycles">Cycles when each item is kneaded on its own; null when no bowl is set.</param>
/// <param name="MergedCycles">Cycles when the dough is kneaded together; null when no bowl is set.</param>
/// <param name="Explanation">Why the kneads can or cannot be merged.</param>
public sealed record KneadingGroupDto(
    Guid RecipeId, string RecipeTitle, IReadOnlyList<Guid> ItemIds, int Loaves, decimal DoughWeightG, int? SeparateCycles,
    int? MergedCycles, string Explanation);

/// <summary>Kneading plan of a batch (US-22).</summary>
/// <param name="BatchId">Batch.</param>
/// <param name="Groups">One group per dough.</param>
/// <param name="TotalSeparateCycles">Cycles without merging.</param>
/// <param name="TotalMergedCycles">Cycles with merging.</param>
public sealed record KneadingPlanDto(Guid BatchId, IReadOnlyList<KneadingGroupDto> Groups, int? TotalSeparateCycles, int? TotalMergedCycles);

/// <summary>A batch to insert and plan.</summary>
/// <param name="Name">Name.</param>
/// <param name="Source">Source.</param>
/// <param name="PlannedStartAt">Planned start.</param>
/// <param name="PriceDate">Price date.</param>
/// <param name="Currency">Currency of the kitchen.</param>
/// <param name="Notes">Notes.</param>
/// <param name="Items">Items.</param>
public sealed record NewBatch(
    string Name, BatchSource Source, DateTimeOffset PlannedStartAt, DateOnly PriceDate, string Currency, string? Notes,
    IReadOnlyList<BatchItemRequest> Items);

/// <summary>What the stage actions need to check.</summary>
/// <param name="StageId">Stage.</param>
/// <param name="Status">Stage status.</param>
/// <param name="BatchStatus">Status of the batch.</param>
/// <param name="RecipeStageId">Recipe stage the plan was built from.</param>
/// <param name="PlannedMinMinutes">Planned minimum.</param>
/// <param name="PlannedMaxMinutes">Planned maximum.</param>
public sealed record StageState(
    Guid StageId, StageStatus Status, BatchStatus BatchStatus, Guid? RecipeStageId, int PlannedMinMinutes, int PlannedMaxMinutes);

/// <summary>Item data used by the kneading planner.</summary>
/// <param name="ItemId">Item.</param>
/// <param name="RecipeId">Recipe.</param>
/// <param name="RecipeTitle">Title.</param>
/// <param name="Loaves">Loaves.</param>
/// <param name="KneadingCycles">Cycles stored by the plan for this item alone.</param>
public sealed record KneadingItem(Guid ItemId, Guid RecipeId, string RecipeTitle, int Loaves, int? KneadingCycles);

/// <summary>Data access for batches; scoped to the owner. Planning runs <c>sp_build_batch_plan</c>.</summary>
public interface IBatchRepository
{
    /// <summary>Calendar page ordered by planned start.</summary>
    Task<PagedResult<BatchSummaryDto>> ListAsync(Guid ownerId, BatchListQuery query, CancellationToken cancellationToken);

    /// <summary>The batch with items and stages, or null.</summary>
    Task<BatchDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the batch and its items and runs <c>sp_build_batch_plan</c> in one transaction; with
    /// <paramref name="save"/> = false the transaction is rolled back and only the plan is returned.
    /// </summary>
    Task<BatchDto> PlanAsync(Guid ownerId, NewBatch batch, bool save, CancellationToken cancellationToken);

    /// <summary>Replaces the items of a planned batch and plans it again; null when not found.</summary>
    Task<BatchDto?> ReplanAsync(Guid ownerId, Guid id, NewBatch batch, CancellationToken cancellationToken);

    /// <summary>Status of the batch, or null.</summary>
    Task<BatchStatus?> GetStatusAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Changes the status (the database checks the life cycle and creates the history record on done).</summary>
    Task SetStatusAsync(Guid ownerId, Guid id, BatchStatus status, CancellationToken cancellationToken);

    /// <summary>History record of a batch.</summary>
    Task<Guid?> GetBakeRecordIdAsync(Guid batchId, CancellationToken cancellationToken);

    /// <summary>Refreshes <c>mv_bake_statistics</c>.</summary>
    Task RefreshStatisticsAsync(CancellationToken cancellationToken);

    /// <summary>The stage of an owned batch, or null.</summary>
    Task<StageState?> GetStageAsync(Guid ownerId, Guid batchId, Guid stageId, CancellationToken cancellationToken);

    /// <summary>Stores the actual start (the database sets the status and moves the batch to in_progress).</summary>
    Task StartStageAsync(Guid stageId, DateTimeOffset at, StageActionRequest action, CancellationToken cancellationToken);

    /// <summary>Stores the actual end (the database sets the status to done).</summary>
    Task CompleteStageAsync(Guid stageId, DateTimeOffset at, StageActionRequest action, CancellationToken cancellationToken);

    /// <summary>Marks the stage as skipped.</summary>
    Task SkipStageAsync(Guid stageId, StageActionRequest action, CancellationToken cancellationToken);

    /// <summary>Runs <c>sp_extend_batch_stage</c>.</summary>
    Task ExtendStageAsync(Guid stageId, int minutes, CancellationToken cancellationToken);

    /// <summary>
    /// Sets the planned durations of a pending stage and shifts the following pending stages and the ready window by
    /// the change of the minimum duration, the same way an extension shifts them.
    /// </summary>
    Task ChangeStageDurationsAsync(Guid stageId, int minMinutes, int maxMinutes, bool overridden, CancellationToken cancellationToken);

    /// <summary>Durations of the recipe stage, or null when it no longer exists.</summary>
    Task<(int Min, int Max)?> GetRecipeStageDurationsAsync(Guid recipeStageId, CancellationToken cancellationToken);

    /// <summary>Items of an owned batch for the kneading plan; null when the batch is not found.</summary>
    Task<IReadOnlyList<KneadingItem>?> GetKneadingItemsAsync(Guid ownerId, Guid batchId, CancellationToken cancellationToken);
}
