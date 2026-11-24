using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.Application.History;

/// <summary>History record in a list (US-33).</summary>
/// <param name="Id">Id.</param>
/// <param name="BatchId">Batch the record was created from.</param>
/// <param name="BakedOn">Bake date.</param>
/// <param name="TotalLoaves">Loaves.</param>
/// <param name="TotalCost">Cost on the bake date; null when prices were missing.</param>
/// <param name="Currency">Currency.</param>
/// <param name="Rating">Rating 1-5.</param>
/// <param name="HasAnalysis">The assistant analysed the result.</param>
/// <param name="RecipeTitles">Recipe titles of the items.</param>
/// <param name="PhotoCount">Crumb photos of the record (US-51).</param>
/// <param name="CoverPhotoId">The first photo of the record, shown in the list; null without photos.</param>
public sealed record BakeRecordSummaryDto(
    Guid Id, Guid? BatchId, DateOnly BakedOn, int TotalLoaves, decimal? TotalCost, string Currency, short? Rating,
    bool HasAnalysis, IReadOnlyList<string> RecipeTitles, int PhotoCount = 0, Guid? CoverPhotoId = null);

/// <summary>Item of a history record.</summary>
/// <param name="Id">Id.</param>
/// <param name="RecipeId">Recipe; null when it was deleted.</param>
/// <param name="RecipeTitle">Recipe title at bake time.</param>
/// <param name="Loaves">Loaves.</param>
/// <param name="Cost">Cost on the bake date.</param>
public sealed record BakeRecordItemDto(Guid Id, Guid? RecipeId, string RecipeTitle, int Loaves, decimal? Cost);

/// <summary>History record created when a batch is completed (US-31, US-32, US-36).</summary>
/// <param name="Id">Id.</param>
/// <param name="BatchId">Batch.</param>
/// <param name="BakedOn">Bake date.</param>
/// <param name="TotalLoaves">Loaves.</param>
/// <param name="TotalCost">Cost on the bake date.</param>
/// <param name="Currency">Currency.</param>
/// <param name="Rating">Rating 1-5.</param>
/// <param name="Comment">Feedback of the baker.</param>
/// <param name="AssistantAnalysis">Analysis by the assistant.</param>
/// <param name="AnalysisAt">Time of the analysis.</param>
/// <param name="Items">Items.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="UpdatedAt">Last change.</param>
/// <param name="Photos">Crumb photos, oldest first (US-51).</param>
public sealed record BakeRecordDto(
    Guid Id, Guid? BatchId, DateOnly BakedOn, int TotalLoaves, decimal? TotalCost, string Currency, short? Rating,
    string? Comment, string? AssistantAnalysis, DateTimeOffset? AnalysisAt, IReadOnlyList<BakeRecordItemDto> Items,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, IReadOnlyList<BakeRecordPhotoDto> Photos);

/// <summary>Query of <c>GET /bake-records</c> (US-33).</summary>
/// <param name="RecipeId">Only records with this recipe.</param>
/// <param name="From">Bake date from (inclusive).</param>
/// <param name="To">Bake date to (inclusive).</param>
/// <param name="MinRating">Minimum rating.</param>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record BakeRecordListQuery(
    Guid? RecipeId = null, DateOnly? From = null, DateOnly? To = null, short? MinRating = null, int Page = 1,
    int PageSize = Paging.DefaultPageSize);

/// <summary>Body of <c>PATCH /bake-records/{id}</c> (US-32): sets the rating and the comment.</summary>
/// <param name="Rating">Rating 1-5, or null to clear.</param>
/// <param name="Comment">Comment, or null to clear.</param>
public sealed record UpdateBakeRecordRequest(short? Rating, string? Comment);

/// <summary>Body of <c>PUT /bake-records/{id}/analysis</c> (US-36, saved by the assistant).</summary>
/// <param name="Analysis">Analysis text.</param>
public sealed record SaveAnalysisRequest(string Analysis);

/// <summary>Planned vs actual times of a stage (<c>v_batch_stage_log</c>, US-36).</summary>
/// <param name="BatchItemId">Batch item.</param>
/// <param name="RecipeTitle">Recipe title.</param>
/// <param name="BatchStageId">Stage.</param>
/// <param name="SortOrder">Order.</param>
/// <param name="StageName">Stage name.</param>
/// <param name="StageTypeCode">Stage type.</param>
/// <param name="StageStatus">Stage status.</param>
/// <param name="PlannedStartAt">Planned start.</param>
/// <param name="PlannedEndMinAt">Planned earliest end.</param>
/// <param name="PlannedEndMaxAt">Planned latest end.</param>
/// <param name="ActualStartAt">Actual start.</param>
/// <param name="ActualEndAt">Actual end.</param>
/// <param name="ExtendedMinutes">Extension, minutes.</param>
/// <param name="ActualTemperatureC">Measured temperature.</param>
/// <param name="StartDelayMinutes">Actual start minus planned start, minutes.</param>
/// <param name="PlannedMinMinutes">Planned minimum.</param>
/// <param name="PlannedMaxMinutes">Planned maximum.</param>
/// <param name="ActualMinutes">Actual duration, minutes.</param>
/// <param name="DeviationMinutes">Minutes outside the planned window (positive = longer).</param>
public sealed record StageLogEntryDto(
    Guid BatchItemId, string RecipeTitle, Guid BatchStageId, int SortOrder, string StageName, string StageTypeCode,
    StageStatus StageStatus, DateTimeOffset PlannedStartAt, DateTimeOffset PlannedEndMinAt, DateTimeOffset PlannedEndMaxAt,
    DateTimeOffset? ActualStartAt, DateTimeOffset? ActualEndAt, int ExtendedMinutes, decimal? ActualTemperatureC,
    decimal? StartDelayMinutes, int PlannedMinMinutes, int PlannedMaxMinutes, int? ActualMinutes, int? DeviationMinutes);

/// <summary>Statistics per recipe (<c>mv_bake_statistics</c>).</summary>
/// <param name="RecipeId">Recipe; null when it was deleted.</param>
/// <param name="RecipeTitle">Title.</param>
/// <param name="BakesCount">Number of bakes.</param>
/// <param name="LoavesTotal">Loaves.</param>
/// <param name="AvgRating">Average rating.</param>
/// <param name="RatedCount">Number of rated bakes.</param>
/// <param name="AvgCostPerLoaf">Average cost per loaf.</param>
/// <param name="FirstBakedOn">First bake.</param>
/// <param name="LastBakedOn">Last bake.</param>
public sealed record BakeStatisticsDto(
    Guid? RecipeId, string RecipeTitle, long BakesCount, long LoavesTotal, decimal? AvgRating, long RatedCount,
    decimal? AvgCostPerLoaf, DateOnly FirstBakedOn, DateOnly LastBakedOn);
