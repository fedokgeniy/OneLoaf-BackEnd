namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>Bake record in the history list (<c>GET /bake-records</c>, US-31, US-33).</summary>
public sealed record BakeRecordSummaryDto(
    Guid Id,
    Guid? BatchId,
    DateOnly BakedOn,
    int TotalLoaves,
    decimal? TotalCost,
    string Currency,
    int? Rating,
    bool HasAnalysis,
    IReadOnlyList<string> RecipeTitles,
    int PhotoCount);

/// <summary>Statistics per recipe (<c>GET /bake-records/statistics</c>).</summary>
public sealed record BakeStatisticsDto(
    Guid? RecipeId,
    string RecipeTitle,
    long BakesCount,
    long LoavesTotal,
    decimal? AvgRating,
    long RatedCount,
    decimal? AvgCostPerLoaf,
    DateOnly FirstBakedOn,
    DateOnly LastBakedOn);

/// <summary>A bake record with rating, comment, analysis, items and photos (US-32, US-36, US-51).</summary>
public sealed record BakeRecordDto(
    Guid Id,
    Guid? BatchId,
    DateOnly BakedOn,
    int TotalLoaves,
    decimal? TotalCost,
    string Currency,
    int? Rating,
    string? Comment,
    string? AssistantAnalysis,
    DateTimeOffset? AnalysisAt,
    IReadOnlyList<BakeRecordItemDto> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<BakeRecordPhotoDto> Photos);

/// <summary>One recipe of a bake record.</summary>
public sealed record BakeRecordItemDto(Guid Id, Guid? RecipeId, string RecipeTitle, int Loaves, decimal? Cost);

/// <summary>Crumb photo (US-51); <see cref="Url"/> is relative to the Core API.</summary>
public sealed record BakeRecordPhotoDto(Guid Id, string ContentType, string Url, DateTimeOffset CreatedAt);

/// <summary><c>PATCH /bake-records/{id}</c> body (US-32): rating 1–5 and comment.</summary>
public sealed record UpdateBakeRecordRequest(int? Rating, string? Comment);

/// <summary>Planned versus actual stage of a bake (<c>GET /bake-records/{id}/stage-log</c>); deviations come from the API.</summary>
public sealed record StageLogEntryDto(
    Guid BatchItemId,
    string RecipeTitle,
    Guid BatchStageId,
    int SortOrder,
    string StageName,
    string StageTypeCode,
    string StageStatus,
    DateTimeOffset PlannedStartAt,
    DateTimeOffset PlannedEndMinAt,
    DateTimeOffset PlannedEndMaxAt,
    DateTimeOffset? ActualStartAt,
    DateTimeOffset? ActualEndAt,
    int ExtendedMinutes,
    decimal? ActualTemperatureC,
    decimal? StartDelayMinutes,
    int PlannedMinMinutes,
    int PlannedMaxMinutes,
    int? ActualMinutes,
    int? DeviationMinutes);
