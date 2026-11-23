using FluentValidation;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

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
public sealed record BakeRecordSummaryDto(
    Guid Id, Guid? BatchId, DateOnly BakedOn, int TotalLoaves, decimal? TotalCost, string Currency, short? Rating,
    bool HasAnalysis, IReadOnlyList<string> RecipeTitles);

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
public sealed record BakeRecordDto(
    Guid Id, Guid? BatchId, DateOnly BakedOn, int TotalLoaves, decimal? TotalCost, string Currency, short? Rating,
    string? Comment, string? AssistantAnalysis, DateTimeOffset? AnalysisAt, IReadOnlyList<BakeRecordItemDto> Items,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

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

/// <summary>Data access for the baking history; scoped to the owner.</summary>
public interface IBakeRecordRepository
{
    /// <summary>Page of records, newest first.</summary>
    Task<PagedResult<BakeRecordSummaryDto>> ListAsync(Guid ownerId, BakeRecordListQuery query, CancellationToken cancellationToken);

    /// <summary>The record, or null.</summary>
    Task<BakeRecordDto?> GetAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Sets rating and comment; false when not found.</summary>
    Task<bool> UpdateFeedbackAsync(Guid ownerId, Guid id, short? rating, string? comment, CancellationToken cancellationToken);

    /// <summary>Stores the assistant's analysis; false when not found.</summary>
    Task<bool> SaveAnalysisAsync(Guid ownerId, Guid id, string analysis, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Stage log of the record's batch; null when the record is not found.</summary>
    Task<IReadOnlyList<StageLogEntryDto>?> GetStageLogAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Statistics of the owner.</summary>
    Task<IReadOnlyList<BakeStatisticsDto>> GetStatisticsAsync(Guid ownerId, CancellationToken cancellationToken);
}

/// <summary>Limits of history requests.</summary>
public static class BakeRecordLimits
{
    /// <summary><c>bake_records.comment</c>.</summary>
    public const int CommentMaxLength = 4000;

    /// <summary>Longest analysis text.</summary>
    public const int AnalysisMaxLength = 20_000;
}

/// <summary>Validates <see cref="UpdateBakeRecordRequest"/>.</summary>
public sealed class UpdateBakeRecordRequestValidator : AbstractValidator<UpdateBakeRecordRequest>
{
    /// <summary>Creates the rules.</summary>
    public UpdateBakeRecordRequestValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.Comment).MaximumLength(BakeRecordLimits.CommentMaxLength);
    }
}

/// <summary>Validates <see cref="SaveAnalysisRequest"/>.</summary>
public sealed class SaveAnalysisRequestValidator : AbstractValidator<SaveAnalysisRequest>
{
    /// <summary>Creates the rules.</summary>
    public SaveAnalysisRequestValidator() =>
        RuleFor(x => x.Analysis).NotEmpty().Must(a => !string.IsNullOrWhiteSpace(a)).MaximumLength(BakeRecordLimits.AnalysisMaxLength);
}

/// <summary>Validates <see cref="BakeRecordListQuery"/>.</summary>
public sealed class BakeRecordListQueryValidator : AbstractValidator<BakeRecordListQuery>
{
    /// <summary>Creates the rules.</summary>
    public BakeRecordListQueryValidator()
    {
        this.AddPagingRules(x => x.Page, x => x.PageSize);
        RuleFor(x => x.MinRating).InclusiveBetween((short)1, (short)5);
        RuleFor(x => x.To).GreaterThanOrEqualTo(x => x.From).When(x => x.From.HasValue && x.To.HasValue);
    }
}

/// <summary>Baking history of the current user (US-31 - US-33, US-36).</summary>
public sealed class BakeRecordService(IBakeRecordRepository records, TimeProvider timeProvider, ICurrentUser currentUser)
{
    /// <summary>Message for a missing record.</summary>
    public const string NotFoundMessage = "The bake record was not found.";

    /// <summary>Page of records (US-33).</summary>
    public Task<PagedResult<BakeRecordSummaryDto>> ListAsync(BakeRecordListQuery query, CancellationToken cancellationToken) =>
        records.ListAsync(currentUser.Id, query, cancellationToken);

    /// <summary>One record (US-31).</summary>
    public async Task<BakeRecordDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await records.GetAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Sets rating and comment (US-32).</summary>
    public async Task<BakeRecordDto> UpdateFeedbackAsync(Guid id, UpdateBakeRecordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var comment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        if (!await records.UpdateFeedbackAsync(currentUser.Id, id, request.Rating, comment, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Stores the assistant's analysis (US-36).</summary>
    public async Task<BakeRecordDto> SaveAnalysisAsync(Guid id, SaveAnalysisRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!await records.SaveAnalysisAsync(currentUser.Id, id, request.Analysis.Trim(), timeProvider.GetUtcNow(), cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Planned vs actual stage times of the record's batch (US-36).</summary>
    public async Task<IReadOnlyList<StageLogEntryDto>> GetStageLogAsync(Guid id, CancellationToken cancellationToken) =>
        await records.GetStageLogAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Statistics per recipe.</summary>
    public Task<IReadOnlyList<BakeStatisticsDto>> GetStatisticsAsync(CancellationToken cancellationToken) =>
        records.GetStatisticsAsync(currentUser.Id, cancellationToken);
}
