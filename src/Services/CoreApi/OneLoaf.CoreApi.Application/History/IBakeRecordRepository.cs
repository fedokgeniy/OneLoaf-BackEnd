using OneLoaf.CoreApi.Application.Common;

namespace OneLoaf.CoreApi.Application.History;

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
