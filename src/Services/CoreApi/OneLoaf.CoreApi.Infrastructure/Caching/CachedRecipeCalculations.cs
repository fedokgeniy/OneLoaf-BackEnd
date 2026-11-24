using System.Globalization;
using Microsoft.Extensions.Caching.Hybrid;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Infrastructure.Persistence;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Infrastructure.Caching;

/// <summary>
/// Results of the database calculation functions through the cache, per user and arguments (10 min). Every
/// successful write request of the user removes the user's tag, so a changed recipe, price or kitchen setting is
/// never served from the cache.
/// </summary>
public sealed class CachedRecipeCalculations(RecipeCalculations inner, HybridCache cache, CacheMetrics metrics, ICurrentUser currentUser)
    : IRecipeCalculations
{
    private const string Area = "calculation";

    /// <inheritdoc />
    public async Task<IReadOnlyList<CostLine>> GetCostAsync(Guid recipeId, int loaves, DateOnly asOf, CancellationToken cancellationToken) =>
        await GetAsync<List<CostLine>>("cost", recipeId, string.Create(CultureInfo.InvariantCulture, $"{loaves}:{asOf:yyyyMMdd}"),
            async ct => [.. await inner.GetCostAsync(recipeId, loaves, asOf, ct)], cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TimelineStageDto>> GetTimelineAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken) =>
        await GetAsync<List<TimelineStageDto>>("timeline", recipeId, Instant(start),
            async ct => [.. await inner.GetTimelineAsync(recipeId, start, ct)], cancellationToken);

    /// <inheritdoc />
    public Task<TimeWindow> GetReadyWindowAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken) =>
        GetAsync("ready", recipeId, Instant(start), ct => inner.GetReadyWindowAsync(recipeId, start, ct), cancellationToken);

    /// <inheritdoc />
    public Task<TimeWindow> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken cancellationToken) =>
        GetAsync("start", recipeId, Instant(readyAt), ct => inner.GetStartWindowAsync(recipeId, readyAt, ct), cancellationToken);

    /// <inheritdoc />
    public Task<ProductionFigures> GetProductionFiguresAsync(Guid recipeId, int loaves, CancellationToken cancellationToken) =>
        GetAsync("figures", recipeId, loaves.ToString(CultureInfo.InvariantCulture),
            ct => inner.GetProductionFiguresAsync(recipeId, loaves, ct), cancellationToken);

    private Task<T> GetAsync<T>(string kind, Guid recipeId, string arguments, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        return metrics.GetOrCreateAsync(cache, CacheKeys.Calculation(userId, kind, recipeId, arguments), Area, load,
            CacheLifetimes.Calculation, [CacheKeys.UserCalculationsTag(userId)], cancellationToken);
    }

    // The offset is part of the key: results keep the offset of the request.
    private static string Instant(DateTimeOffset value) =>
        string.Create(CultureInfo.InvariantCulture, $"{value.UtcTicks}{value.Offset.TotalMinutes:+0;-0}");
}
