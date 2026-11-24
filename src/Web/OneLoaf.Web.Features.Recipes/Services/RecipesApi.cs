using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Recipes.Services;

/// <summary>Server-side filter of the recipe list (US-05, US-37).</summary>
/// <param name="Search">Part of the title.</param>
/// <param name="IncludeArchived">Also list archived recipes.</param>
/// <param name="MinProteinPercent">Only recipes with a flour of at least this protein.</param>
public sealed record RecipeQuery(string Search, bool IncludeArchived, decimal? MinProteinPercent)
{
    /// <summary>No filter.</summary>
    public static RecipeQuery None { get; } = new(string.Empty, false, null);

    /// <summary>Any filter is set.</summary>
    public bool IsFiltered => Search.Length > 0 || IncludeArchived || MinProteinPercent is not null;
}

/// <summary>Typed client of the recipe endpoints of the Core API.</summary>
public interface IRecipesApi
{
    /// <summary><c>GET recipes/{id}/photo</c>: the image with the bearer token.</summary>
    Task<ApiResult<FileDownload>> GetPhotoAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>PUT recipes/{id}/photo</c>: 422 for a file that is not JPEG, PNG or WebP.</summary>
    Task<ApiResult> SetPhotoAsync(Guid id, PhotoFile photo, CancellationToken ct = default);

    /// <summary><c>DELETE recipes/{id}/photo</c>.</summary>
    Task<ApiResult> DeletePhotoAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>GET recipes</c>: one page of the caller's recipes.</summary>
    Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListAsync(RecipeQuery query, int page, CancellationToken ct = default);

    /// <summary><c>GET recipes/{id}</c>: the recipe with ingredients and stages; 404 when missing or not the caller's.</summary>
    Task<ApiResult<RecipeDto>> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>GET recipes/{id}/scaled?loaves=</c> (US-06).</summary>
    Task<ApiResult<ScaledRecipeDto>> ScaleAsync(Guid id, int loaves, CancellationToken ct = default);

    /// <summary><c>GET recipes/{id}/cost?loaves=&amp;asOf=</c> (US-14, US-15).</summary>
    Task<ApiResult<CostBreakdownDto>> GetCostAsync(Guid id, int loaves, DateOnly? asOf, CancellationToken ct = default);

    /// <summary><c>GET recipes/{id}/production-figures?loaves=</c> (US-17, US-18).</summary>
    Task<ApiResult<ProductionFiguresDto>> GetProductionFiguresAsync(Guid id, int loaves, CancellationToken ct = default);

    /// <summary><c>GET recipes/{id}/timeline?start=</c> (US-16).</summary>
    Task<ApiResult<TimelineDto>> GetTimelineAsync(Guid id, DateTimeOffset start, CancellationToken ct = default);

    /// <summary><c>POST recipes</c> (US-05): 201 with the recipe, 400 field errors.</summary>
    Task<ApiResult<RecipeDto>> CreateAsync(RecipeRequest request, CancellationToken ct = default);

    /// <summary><c>PUT recipes/{id}</c>: 200, 400, 404, 422 (a rule of a variant).</summary>
    Task<ApiResult<RecipeDto>> UpdateAsync(Guid id, RecipeRequest request, CancellationToken ct = default);

    /// <summary><c>DELETE recipes/{id}</c>: 204, 409 when it has variants or batches (archive instead).</summary>
    Task<ApiResult> DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary><c>POST recipes/{id}/archive</c> or <c>/unarchive</c>.</summary>
    Task<ApiResult> SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default);

    /// <summary><c>POST recipes/{id}/variants</c> (US-07).</summary>
    Task<ApiResult<RecipeDto>> CreateVariantAsync(Guid id, CreateVariantRequest request, CancellationToken ct = default);

    /// <summary><c>GET recipes/{id}/comparison</c> (US-08): 422 when the recipe is not a variant.</summary>
    Task<ApiResult<RecipeComparisonDto>> CompareAsync(Guid id, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class RecipesApi(ApiClientFactory api) : IRecipesApi
{
    /// <summary>Page size of the recipe list.</summary>
    public const int PageSize = 20;

    /// <inheritdoc />
    public Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListAsync(RecipeQuery query, int page, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var url = Url.Build("api/v1/recipes",
            ("search", query.Search),
            ("includeArchived", query.IncludeArchived ? true : null),
            ("minProteinPercent", query.MinProteinPercent),
            ("page", page),
            ("pageSize", PageSize));
        return api.Core.GetAsync<PagedResult<RecipeSummaryDto>>(url, ct);
    }

    /// <inheritdoc />
    public Task<ApiResult<RecipeDto>> GetAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<RecipeDto>($"api/v1/recipes/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult<ScaledRecipeDto>> ScaleAsync(Guid id, int loaves, CancellationToken ct = default) =>
        api.Core.GetAsync<ScaledRecipeDto>(Url.Build($"api/v1/recipes/{id}/scaled", ("loaves", loaves)), ct);

    /// <inheritdoc />
    public Task<ApiResult<CostBreakdownDto>> GetCostAsync(Guid id, int loaves, DateOnly? asOf, CancellationToken ct = default) =>
        api.Core.GetAsync<CostBreakdownDto>(Url.Build($"api/v1/recipes/{id}/cost", ("loaves", loaves), ("asOf", asOf)), ct);

    /// <inheritdoc />
    public Task<ApiResult<ProductionFiguresDto>> GetProductionFiguresAsync(Guid id, int loaves, CancellationToken ct = default) =>
        api.Core.GetAsync<ProductionFiguresDto>(Url.Build($"api/v1/recipes/{id}/production-figures", ("loaves", loaves)), ct);

    /// <inheritdoc />
    public Task<ApiResult<TimelineDto>> GetTimelineAsync(Guid id, DateTimeOffset start, CancellationToken ct = default) =>
        api.Core.GetAsync<TimelineDto>(Url.Build($"api/v1/recipes/{id}/timeline", ("start", start)), ct);

    /// <inheritdoc />
    public Task<ApiResult<RecipeDto>> CreateAsync(RecipeRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<RecipeRequest, RecipeDto>("api/v1/recipes", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<RecipeDto>> UpdateAsync(Guid id, RecipeRequest request, CancellationToken ct = default) =>
        api.Core.PutAsync<RecipeRequest, RecipeDto>($"api/v1/recipes/{id}", request, ct);

    /// <inheritdoc />
    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken ct = default) =>
        api.Core.DeleteAsync($"api/v1/recipes/{id}", ct);

    /// <inheritdoc />
    public Task<ApiResult> SetArchivedAsync(Guid id, bool archived, CancellationToken ct = default) =>
        api.Core.PostAsync($"api/v1/recipes/{id}/{(archived ? "archive" : "unarchive")}", ct);

    /// <inheritdoc />
    public Task<ApiResult<RecipeDto>> CreateVariantAsync(Guid id, CreateVariantRequest request, CancellationToken ct = default) =>
        api.Core.PostAsync<CreateVariantRequest, RecipeDto>($"api/v1/recipes/{id}/variants", request, ct);

    /// <inheritdoc />
    public Task<ApiResult<RecipeComparisonDto>> CompareAsync(Guid id, CancellationToken ct = default) =>
        api.Core.GetAsync<RecipeComparisonDto>($"api/v1/recipes/{id}/comparison", ct);

    /// <inheritdoc />
    public Task<ApiResult<FileDownload>> GetPhotoAsync(Guid id, CancellationToken ct = default) =>
        api.Core.DownloadAsync($"api/v1/recipes/{id}/photo", ct);

    /// <inheritdoc />
    public async Task<ApiResult> SetPhotoAsync(Guid id, PhotoFile photo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(photo);
        using var stream = new MemoryStream(photo.Content);
        return await api.Core.PutFileAsync($"api/v1/recipes/{id}/photo", stream, photo.FileName, photo.ContentType, ct);
    }

    /// <inheritdoc />
    public Task<ApiResult> DeletePhotoAsync(Guid id, CancellationToken ct = default) =>
        api.Core.DeleteAsync($"api/v1/recipes/{id}/photo", ct);
}
