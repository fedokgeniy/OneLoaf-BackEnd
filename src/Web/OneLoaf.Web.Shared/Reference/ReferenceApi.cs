using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Shared.Reference;

/// <summary>Reference data of the Core API (units, stage types, currencies, categories, flour types), cached for the session.</summary>
public interface IReferenceApi
{
    /// <summary><c>GET reference/units</c>.</summary>
    Task<ApiResult<IReadOnlyList<UnitDto>>> GetUnitsAsync(CancellationToken ct = default);

    /// <summary><c>GET reference/stage-types</c>.</summary>
    Task<ApiResult<IReadOnlyList<StageTypeDto>>> GetStageTypesAsync(CancellationToken ct = default);

    /// <summary><c>GET reference/currencies</c>.</summary>
    Task<ApiResult<IReadOnlyList<CurrencyDto>>> GetCurrenciesAsync(CancellationToken ct = default);

    /// <summary><c>GET reference/ingredient-categories</c>.</summary>
    Task<ApiResult<IReadOnlyList<IngredientCategoryDto>>> GetIngredientCategoriesAsync(CancellationToken ct = default);

    /// <summary><c>GET reference/flour-types</c>.</summary>
    Task<ApiResult<IReadOnlyList<FlourTypeDto>>> GetFlourTypesAsync(CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ReferenceApi(ApiClientFactory api) : IReferenceApi
{
    private readonly Dictionary<string, object> _cache = [];

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<UnitDto>>> GetUnitsAsync(CancellationToken ct = default) =>
        Cached<UnitDto>("api/v1/reference/units", ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<StageTypeDto>>> GetStageTypesAsync(CancellationToken ct = default) =>
        Cached<StageTypeDto>("api/v1/reference/stage-types", ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<CurrencyDto>>> GetCurrenciesAsync(CancellationToken ct = default) =>
        Cached<CurrencyDto>("api/v1/reference/currencies", ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<IngredientCategoryDto>>> GetIngredientCategoriesAsync(CancellationToken ct = default) =>
        Cached<IngredientCategoryDto>("api/v1/reference/ingredient-categories", ct);

    /// <inheritdoc />
    public Task<ApiResult<IReadOnlyList<FlourTypeDto>>> GetFlourTypesAsync(CancellationToken ct = default) =>
        Cached<FlourTypeDto>("api/v1/reference/flour-types", ct);

    /// <summary>Only successful answers are cached; a failure is retried on the next call.</summary>
    private async Task<ApiResult<IReadOnlyList<T>>> Cached<T>(string path, CancellationToken ct)
    {
        if (_cache.TryGetValue(path, out var hit))
        {
            return ApiResult<IReadOnlyList<T>>.Ok((IReadOnlyList<T>)hit);
        }

        var result = await api.Core.GetAsync<IReadOnlyList<T>>(path, ct);
        if (result is ApiResult<IReadOnlyList<T>>.Success { Value: var value })
        {
            _cache[path] = value;
        }

        return result;
    }
}
