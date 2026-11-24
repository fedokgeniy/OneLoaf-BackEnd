using OneLoaf.CoreApi.Application.Common;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.History;

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
