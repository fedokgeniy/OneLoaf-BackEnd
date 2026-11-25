using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Library.Services;

/// <summary>Typed client of the public library, publishing and moderation (US-39–42, LR-11).</summary>
public interface ILibraryApi
{
    /// <summary>Page size of the library and the comments.</summary>
    const int PageSize = 20;

    /// <summary><c>GET library?search=&amp;page=</c> (anonymous).</summary>
    Task<ApiResult<PagedResult<LibraryItemDto>>> SearchAsync(string? search, int page, CancellationToken ct = default);

    /// <summary><c>GET library/{id}</c> (anonymous; with a token it also says <c>isMine</c> and <c>myReaction</c>).</summary>
    Task<ApiResult<PublishedRecipeDto>> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>GET library/{id}/comments?page=</c>.</summary>
    Task<ApiResult<PagedResult<CommentDto>>> ListCommentsAsync(Guid id, int page, CancellationToken ct = default);

    /// <summary><c>POST library/{id}/comments</c>.</summary>
    Task<ApiResult<CommentDto>> AddCommentAsync(Guid id, string body, CancellationToken ct = default);

    /// <summary><c>DELETE library/{id}/comments/{commentId}</c> (own comment).</summary>
    Task<ApiResult> RemoveCommentAsync(Guid id, Guid commentId, CancellationToken ct = default);

    /// <summary><c>PUT</c> or <c>DELETE library/{id}/reactions/me</c>; <see langword="null"/> removes it.</summary>
    Task<ApiResult> SetReactionAsync(Guid id, string? reaction, CancellationToken ct = default);

    /// <summary><c>POST library/{id}/copy</c>: the new recipe of the user (US-42).</summary>
    Task<ApiResult<RecipeDto>> CopyAsync(Guid id, CopyPublicationRequest request, CancellationToken ct = default);

    /// <summary><c>GET library/{id}/photo</c>: the photo of a published recipe.</summary>
    Task<ApiResult<FileDownload>> GetPhotoAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>GET ingredients</c>: the own catalog, to choose what replaces an ingredient of a published recipe.</summary>
    Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(CancellationToken ct = default);

    /// <summary><c>POST library/{id}/reports</c> (LR-11).</summary>
    Task<ApiResult<ReportCreatedResponse>> ReportAsync(Guid id, ReportRequest request, CancellationToken ct = default);

    /// <summary><c>POST library/{id}/unpublish</c> (author only).</summary>
    Task<ApiResult> UnpublishAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST recipes/{recipeId}/publish</c> (US-39): a snapshot of the recipe as it is now.</summary>
    Task<ApiResult<PublishResponse>> PublishAsync(Guid recipeId, CancellationToken ct = default);

    /// <summary><c>GET recipes/{id}</c>: title of the recipe to publish.</summary>
    Task<ApiResult<RecipeDto>> GetRecipeAsync(Guid recipeId, CancellationToken ct = default);

    /// <summary><c>GET admin/reports?status=&amp;page=</c> (administrators).</summary>
    Task<ApiResult<PagedResult<ReportDto>>> ListReportsAsync(string status, int page, CancellationToken ct = default);

    /// <summary><c>POST admin/reports/{id}/resolve</c>: 409 when already decided.</summary>
    Task<ApiResult> ResolveReportAsync(Guid reportId, ResolveReportRequest request, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class LibraryApi(ApiClientFactory api) : ILibraryApi
{
    /// <inheritdoc />
    public Task<ApiResult<PagedResult<LibraryItemDto>>> SearchAsync(string? search, int page, CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<LibraryItemDto>>(Url.Build("api/v1/library",
            ("search", string.IsNullOrWhiteSpace(search) ? null : search.Trim()), ("page", page), ("pageSize", ILibraryApi.PageSize)), ct);

    /// <inheritdoc />
    public Task<ApiResult<PublishedRecipeDto>> GetAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<PublishedRecipeDto>($"api/v1/library/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<CommentDto>>> ListCommentsAsync(Guid id, int page, CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<CommentDto>>(Url.Build($"api/v1/library/{id}/comments", ("page", page), ("pageSize", ILibraryApi.PageSize)), ct);

    /// <inheritdoc />
    public Task<ApiResult<CommentDto>> AddCommentAsync(Guid id, string body, CancellationToken ct = default) =>
        api.Core.PostAsync<CommentRequest, CommentDto>($"api/v1/library/{id}/comments", new CommentRequest(body), ct);

    /// <inheritdoc />
    public Task<ApiResult> RemoveCommentAsync(Guid id, Guid commentId, CancellationToken ct = default) =>
        api.Core.DeleteAsync($"api/v1/library/{id}/comments/{commentId}", ct);

    /// <inheritdoc />
    public Task<ApiResult> SetReactionAsync(Guid id, string? reaction, CancellationToken ct = default) =>
        reaction is null
            ? api.Core.DeleteAsync($"api/v1/library/{id}/reactions/me", ct)
            : api.Core.PutAsync($"api/v1/library/{id}/reactions/me", new ReactionRequest(reaction), ct);

    /// <inheritdoc />
    public Task<ApiResult<RecipeDto>> CopyAsync(Guid id, CopyPublicationRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<CopyPublicationRequest, RecipeDto>($"api/v1/library/{id}/copy", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<FileDownload>> GetPhotoAsync(Guid id, CancellationToken ct = default) =>
        api.Core.DownloadAsync($"api/v1/library/{id}/photo", ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<IngredientDto>>> ListIngredientsAsync(CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<IngredientDto>>("api/v1/ingredients?page=1&pageSize=100", ct);

    /// <inheritdoc />
    public Task<ApiResult<ReportCreatedResponse>> ReportAsync(Guid id, ReportRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<ReportRequest, ReportCreatedResponse>($"api/v1/library/{id}/reports", request, ct);

    /// <inheritdoc />
    public Task<ApiResult> UnpublishAsync(Guid id, CancellationToken ct = default) =>
        api.Core.PostAsync($"api/v1/library/{id}/unpublish", ct);

    /// <inheritdoc />
    public Task<ApiResult<PublishResponse>> PublishAsync(Guid recipeId, CancellationToken ct = default) =>
        api.Core.PostEmptyAsync<PublishResponse>($"api/v1/recipes/{recipeId}/publish", ct);

    /// <inheritdoc />
    public Task<ApiResult<RecipeDto>> GetRecipeAsync(Guid recipeId, CancellationToken ct = default) =>
        api.Core.GetAsync<RecipeDto>($"api/v1/recipes/{recipeId}", ct);

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<ReportDto>>> ListReportsAsync(string status, int page, CancellationToken ct = default) =>
        api.Core.GetAsync<PagedResult<ReportDto>>(Url.Build("api/v1/admin/reports", ("status", status), ("page", page), ("pageSize", ILibraryApi.PageSize)), ct);

    /// <inheritdoc />
    public Task<ApiResult> ResolveReportAsync(Guid reportId, ResolveReportRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync($"api/v1/admin/reports/{reportId}/resolve", request, ct);
}
