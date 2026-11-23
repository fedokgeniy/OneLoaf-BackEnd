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
}
