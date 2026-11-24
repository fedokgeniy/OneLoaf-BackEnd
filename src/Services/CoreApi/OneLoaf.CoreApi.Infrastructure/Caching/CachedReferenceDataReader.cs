using Microsoft.Extensions.Caching.Hybrid;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.CoreApi.Infrastructure.Persistence;

namespace OneLoaf.CoreApi.Infrastructure.Caching;

/// <summary>Reference tables through the cache (24 h).</summary>
public sealed class CachedReferenceDataReader(ReferenceDataReader inner, HybridCache cache, CacheMetrics metrics) : IReferenceDataReader
{
    private const string Area = "reference";

    /// <inheritdoc />
    public Task<IReadOnlyList<UnitDto>> GetUnitsAsync(CancellationToken cancellationToken) =>
        GetAsync("units", inner.GetUnitsAsync, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken cancellationToken) =>
        GetAsync("currencies", inner.GetCurrenciesAsync, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<StageTypeDto>> GetStageTypesAsync(CancellationToken cancellationToken) =>
        GetAsync("stage-types", inner.GetStageTypesAsync, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<IngredientCategoryDto>> GetIngredientCategoriesAsync(CancellationToken cancellationToken) =>
        GetAsync("ingredient-categories", inner.GetIngredientCategoriesAsync, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<FlourTypeDto>> GetFlourTypesAsync(CancellationToken cancellationToken) =>
        GetAsync("flour-types", inner.GetFlourTypesAsync, cancellationToken);

    private async Task<IReadOnlyList<T>> GetAsync<T>(
        string table, Func<CancellationToken, Task<IReadOnlyList<T>>> load, CancellationToken cancellationToken) =>
        await metrics.GetOrCreateAsync<List<T>>(cache, CacheKeys.Reference(table), Area,
            async ct => [.. await load(ct)], CacheLifetimes.Reference, null, cancellationToken);
}
