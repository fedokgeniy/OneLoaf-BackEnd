using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Assistant;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.History.Services;

/// <summary>Filter of the history list (US-33).</summary>
public sealed record HistoryQuery(Guid? RecipeId, int? MinRating, DateOnly? From, DateOnly? To)
{
    /// <summary>No filter.</summary>
    public static HistoryQuery None { get; } = new(null, null, null, null);

    /// <summary>Any filter set.</summary>
    public bool IsFiltered => RecipeId is not null || MinRating is not null || From is not null || To is not null;
}

/// <summary>Typed client of the bake record endpoints and the assistant's bake analysis (US-31–33, US-36, US-51).</summary>
public interface IHistoryApi
{
    /// <summary>Page size of the history list.</summary>
    const int PageSize = 20;

    /// <summary><c>GET bake-records?recipeId=&amp;minRating=&amp;from=&amp;to=&amp;page=</c>.</summary>
    Task<ApiResult<PagedResult<BakeRecordSummaryDto>>> ListAsync(HistoryQuery query, int page, CancellationToken ct = default);

    /// <summary><c>GET bake-records/statistics</c>.</summary>
    Task<ApiResult<IReadOnlyList<BakeStatisticsDto>>> GetStatisticsAsync(CancellationToken ct = default);

    /// <summary><c>GET bake-records/{id}</c>.</summary>
    Task<ApiResult<BakeRecordDto>> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>PATCH bake-records/{id}</c>: rating and comment (US-32).</summary>
    Task<ApiResult<BakeRecordDto>> RateAsync(Guid id, UpdateBakeRecordRequest request, CancellationToken ct = default);

    /// <summary><c>GET bake-records/{id}/stage-log</c>.</summary>
    Task<ApiResult<IReadOnlyList<StageLogEntryDto>>> GetStageLogAsync(Guid id, CancellationToken ct = default);

    /// <summary>Photo bytes from the authenticated photo URL.</summary>
    Task<ApiResult<FileDownload>> GetPhotoAsync(string url, CancellationToken ct = default);

    /// <summary><c>POST bake-records/{id}/photos</c> (US-51): 422 for a file that is not JPEG, PNG or WebP.</summary>
    Task<ApiResult<BakeRecordPhotoDto>> UploadPhotoAsync(Guid id, Stream file, string fileName, string contentType, CancellationToken ct = default);

    /// <summary><c>DELETE bake-records/{id}/photos/{photoId}</c>.</summary>
    Task<ApiResult> DeletePhotoAsync(Guid id, Guid photoId, CancellationToken ct = default);

    /// <summary>Assistant <c>POST bake-records/{id}/analysis</c> (US-36): stored in the record; 422 without rating or comment.</summary>
    Task<ApiResult<AnalysisDto>> AnalyseAsync(Guid id, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class HistoryApi(ApiClientFactory api) : IHistoryApi
{
    /// <inheritdoc />
    public Task<ApiResult<PagedResult<BakeRecordSummaryDto>>> ListAsync(HistoryQuery query, int page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return api.Core.GetAsync<PagedResult<BakeRecordSummaryDto>>(Url.Build("api/v1/bake-records",
            ("recipeId", query.RecipeId), ("minRating", query.MinRating), ("from", query.From), ("to", query.To),
            ("page", page), ("pageSize", IHistoryApi.PageSize)), ct);
    }

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<BakeStatisticsDto>>> GetStatisticsAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<IReadOnlyList<BakeStatisticsDto>>("api/v1/bake-records/statistics", ct);

    /// <inheritdoc />
    public Task<ApiResult<BakeRecordDto>> GetAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<BakeRecordDto>($"api/v1/bake-records/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<BakeRecordDto>> RateAsync(Guid id, UpdateBakeRecordRequest request, CancellationToken ct = default) =>
        api.Core.PatchAsync<UpdateBakeRecordRequest, BakeRecordDto>($"api/v1/bake-records/{id}", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<StageLogEntryDto>>> GetStageLogAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<IReadOnlyList<StageLogEntryDto>>($"api/v1/bake-records/{id}/stage-log", ct);

    /// <inheritdoc />
    public Task<ApiResult<FileDownload>> GetPhotoAsync(string url, CancellationToken ct = default) =>
        api.Core.DownloadAsync(url, ct);

    /// <inheritdoc />
    public Task<ApiResult<BakeRecordPhotoDto>> UploadPhotoAsync(Guid id, Stream file, string fileName, string contentType, CancellationToken ct = default) =>
        api.Core.PostFileAsync<BakeRecordPhotoDto>($"api/v1/bake-records/{id}/photos", file, fileName, contentType, ct);

    /// <inheritdoc />
    public Task<ApiResult> DeletePhotoAsync(Guid id, Guid photoId, CancellationToken ct = default) =>
        api.Core.DeleteAsync($"api/v1/bake-records/{id}/photos/{photoId}", ct);

    /// <inheritdoc />
    public Task<ApiResult<AnalysisDto>> AnalyseAsync(Guid id, CancellationToken ct = default) =>
        api.Assistant.PostEmptyAsync<AnalysisDto>($"api/v1/bake-records/{id}/analysis", ct);
}
