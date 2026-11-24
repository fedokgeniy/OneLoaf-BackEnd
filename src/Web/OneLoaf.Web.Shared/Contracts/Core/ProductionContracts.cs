namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>Batch status codes (<c>planned</c>, <c>in_progress</c>, <c>done</c>, <c>cancelled</c>).</summary>
public static class BatchStatuses
{
    /// <summary>Planned, not started.</summary>
    public const string Planned = "planned";

    /// <summary>A stage has started.</summary>
    public const string InProgress = "in_progress";

    /// <summary>Completed.</summary>
    public const string Done = "done";

    /// <summary>Cancelled.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>Stage status codes (<c>pending</c>, <c>running</c>, <c>done</c>, <c>skipped</c>).</summary>
public static class StageStatuses
{
    /// <summary>Not started.</summary>
    public const string Pending = "pending";

    /// <summary>Running.</summary>
    public const string Running = "running";

    /// <summary>Done.</summary>
    public const string Done = "done";

    /// <summary>Skipped.</summary>
    public const string Skipped = "skipped";
}

/// <summary>Batch in a list or calendar (<c>GET /batches</c>, US-26).</summary>
public sealed record BatchSummaryDto(
    Guid Id, string Name, string Status, DateTimeOffset PlannedStartAt, DateTimeOffset? PlannedReadyFrom,
    DateTimeOffset? PlannedReadyTo, int TotalLoaves);

/// <summary><c>POST /batches</c>, <c>/batches/preview</c>, <c>PUT /batches/{id}</c> body (US-20). <see cref="Source"/>: manual or assistant.</summary>
public sealed record BatchRequest(
    string Name, DateTimeOffset PlannedStartAt, IReadOnlyList<BatchItemRequest> Items, DateOnly? PriceDate, string? Notes, string? Source,
    decimal? IngredientScale = null);

/// <summary>An ingredient the stock does not cover for a batch; quantities in <see cref="UnitCode"/>.</summary>
public sealed record StockShortageDto(Guid IngredientId, string Name, string UnitCode, decimal Needed, decimal Available, decimal Missing);

/// <summary>
/// Stock check of a batch plan. <see cref="MaxLoaves"/>: loaves the stock covers at full quantities (one-item batches);
/// <see cref="MaxScale"/>: share of the dough the stock covers with the planned loaves. Both come from the server.
/// </summary>
public sealed record BatchStockDto(IReadOnlyList<StockShortageDto> Shortages, int? MaxLoaves, decimal? MaxScale);

/// <summary>One recipe of a batch; a starter reminder is sent <see cref="StarterReminderMinutes"/> before kneading (US-29).</summary>
public sealed record BatchItemRequest(Guid RecipeId, int Loaves, Guid? StarterId, int? StarterReminderMinutes);

/// <summary>A batch with planned figures and its stages (US-20–25).</summary>
public sealed record BatchDto(
    Guid Id,
    string Name,
    string Status,
    string Source,
    DateTimeOffset PlannedStartAt,
    DateTimeOffset? PlannedReadyFrom,
    DateTimeOffset? PlannedReadyTo,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? CompletedAt,
    DateOnly PriceDate,
    decimal? TotalCost,
    string Currency,
    string? Notes,
    IReadOnlyList<BatchItemDto> Items,
    Guid? BakeRecordId,
    decimal IngredientScale = 1m,
    BatchStockDto? Stock = null);

/// <summary>One recipe of a batch with its figures and stages.</summary>
public sealed record BatchItemDto(
    Guid Id,
    Guid RecipeId,
    string RecipeTitle,
    int Loaves,
    Guid? StarterId,
    decimal? StarterAmountG,
    int? KneadingCycles,
    int? StarterReminderMinutes,
    int SortOrder,
    IReadOnlyList<BatchStageDto> Stages);

/// <summary>A planned and actual stage of a batch item.</summary>
public sealed record BatchStageDto(
    Guid Id,
    int SortOrder,
    string Name,
    string StageTypeCode,
    int PlannedMinMinutes,
    int PlannedMaxMinutes,
    DateTimeOffset PlannedStartAt,
    DateTimeOffset PlannedEndMinAt,
    DateTimeOffset PlannedEndMaxAt,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? ActualEndAt,
    string Status,
    int ExtendedMinutes,
    bool IsOverridden,
    decimal? ActualTemperatureC,
    string? Note);

/// <summary><c>POST /batches/{id}/complete</c> response (US-31): the bake record created for the history.</summary>
public sealed record CompleteBatchResponse(Guid? BakeRecordId);

/// <summary><c>POST /batches/{id}/save-as-variant</c> body (US-25).</summary>
public sealed record SaveAsVariantRequest(Guid? ItemId, string? Title);

/// <summary><c>POST /batches/{id}/save-as-variant</c> response.</summary>
public sealed record SaveAsVariantResponse(RecipeDto Recipe, IReadOnlyList<StageChangeDto> Changes);

/// <summary>A stage duration taken over from the batch into the variant.</summary>
public sealed record StageChangeDto(string Name, int RecipeMinMinutes, int RecipeMaxMinutes, int VariantMinMinutes, int VariantMaxMinutes, string Source);

/// <summary>Kneading cycles per recipe, separate and merged (<c>GET /batches/{id}/kneading-plan</c>, US-22).</summary>
public sealed record KneadingPlanDto(Guid BatchId, IReadOnlyList<KneadingGroupDto> Groups, int? TotalSeparateCycles, int? TotalMergedCycles);

/// <summary>Kneading group with the API's explanation.</summary>
public sealed record KneadingGroupDto(
    Guid RecipeId, string RecipeTitle, IReadOnlyList<Guid> ItemIds, int Loaves, decimal DoughWeightG,
    int? SeparateCycles, int? MergedCycles, string Explanation);

/// <summary>Start / complete / skip body: optional actual temperature and note.</summary>
public sealed record StageActionRequest(decimal? ActualTemperatureC, string? Note);

/// <summary>Extend a running stage (US-24); default 30 minutes.</summary>
public sealed record ExtendStageRequest(int? Minutes);

/// <summary>Adjust the planned durations of a stage (US-21).</summary>
public sealed record StageDurationRequest(int PlannedMinMinutes, int PlannedMaxMinutes);

/// <summary>Sourdough starter (US-27, US-28, US-30). <see cref="FeedingMode"/>: regular, weekly or fridge.</summary>
public sealed record StarterDto(
    Guid Id,
    string Name,
    Guid? FlourId,
    decimal HydrationPercent,
    string FeedingMode,
    int FeedingIntervalHours,
    decimal? RiseHours,
    bool IsActive,
    string? Notes,
    DateTimeOffset? LastFedAt,
    DateTimeOffset? NextFeedingAt,
    DateTimeOffset? ExpectedPeakAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? FlourIngredientId = null,
    Guid? WaterIngredientId = null,
    decimal? AmountG = null,
    Guid? IngredientId = null,
    decimal UsableShare = 0.667m,
    decimal? UsableG = null,
    decimal? SuggestedKeptG = null);

/// <summary>
/// <c>POST/PUT /starters</c> body. The flour of the starter is an ingredient; feedings take flour and water from the
/// stock of the two ingredients, and <see cref="UsableShare"/> of the fed starter becomes stock when it is ripe.
/// </summary>
public sealed record StarterRequest(
    string Name, decimal HydrationPercent, string FeedingMode, int FeedingIntervalHours, Guid? FlourId, decimal? RiseHours,
    bool IsActive, string? Notes, Guid? FlourIngredientId = null, Guid? WaterIngredientId = null, decimal? UsableShare = null);

/// <summary>A feeding of a starter (US-28).</summary>
public sealed record FeedingDto(
    Guid Id, DateTimeOffset FedAt, decimal FlourG, decimal WaterG, decimal? StarterKeptG, decimal? TemperatureC,
    DateTimeOffset? NextFeedingAt, DateTimeOffset? ExpectedPeakAt, string? Note);

/// <summary><c>POST /starters/{id}/feedings</c> body.</summary>
public sealed record FeedingRequest(
    DateTimeOffset FedAt, decimal FlourG, decimal WaterG, decimal? StarterKeptG, decimal? TemperatureC, string? Note);
