using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

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
}
