using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.CoreApi.Infrastructure.Persistence;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Infrastructure.Caching;

/// <summary>Redis settings (<c>Redis__ConnectionString</c>); without it the cache is in-process only.</summary>
public sealed class RedisOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string.</summary>
    [MaxLength(1024)]
    public string? ConnectionString { get; set; }
}

/// <summary>Cache keys and tags of the Core API; services never build keys themselves.</summary>
public static class CacheKeys
{
    /// <summary>Prefix of every key in the shared Redis.</summary>
    public const string InstanceName = "core:";

    /// <summary>A reference table.</summary>
    public static string Reference(string table) => $"ref:{table}";

    /// <summary>A calculation result of a user.</summary>
    public static string Calculation(Guid userId, string kind, Guid recipeId, string arguments) =>
        $"calc:{userId:N}:{kind}:{recipeId:N}:{arguments}";

    /// <summary>Tag of all calculation results of a user.</summary>
    public static string UserCalculationsTag(Guid userId) => $"user:{userId:N}:calc";
}

/// <summary>Lifetimes of the cached entries.</summary>
public static class CacheLifetimes
{
    /// <summary>Reference tables change only with a migration.</summary>
    public static readonly HybridCacheEntryOptions Reference = new() { Expiration = TimeSpan.FromHours(24), LocalCacheExpiration = TimeSpan.FromHours(1) };

    /// <summary>Calculation results; also removed by tag on any change of the user's data.</summary>
    public static readonly HybridCacheEntryOptions Calculation = new() { Expiration = TimeSpan.FromMinutes(10), LocalCacheExpiration = TimeSpan.FromMinutes(2) };
}

/// <summary>Hit and miss counters of the cache (<c>oneloaf.cache.hits</c>, <c>oneloaf.cache.misses</c>).</summary>
public sealed class CacheMetrics
{
    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;

    /// <summary>Creates the counters on the OneLoaf meter.</summary>
    public CacheMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(Meters.OneLoaf);
        _hits = meter.CreateCounter<long>("oneloaf.cache.hits", description: "Values served from the cache.");
        _misses = meter.CreateCounter<long>("oneloaf.cache.misses", description: "Values computed because they were not cached.");
    }

    /// <summary>Reads through the cache and counts a hit or a miss.</summary>
    public async Task<T> GetOrCreateAsync<T>(
        HybridCache cache, string key, string area, Func<CancellationToken, Task<T>> factory, HybridCacheEntryOptions entryOptions,
        IEnumerable<string>? tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cache);
        var computed = false;
        var value = await cache.GetOrCreateAsync(key, async ct =>
        {
            computed = true;
            return await factory(ct);
        }, entryOptions, tags, cancellationToken);
        (computed ? _misses : _hits).Add(1, new KeyValuePair<string, object?>("area", area));
        return value;
    }
}

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

/// <summary>Registration of the cache.</summary>
public static class CachingExtensions
{
    /// <summary>
    /// HybridCache (in-process L1) with Redis as the shared L2 when <c>Redis__ConnectionString</c> is set, and the
    /// cached decorators of the reference data and the calculations.
    /// </summary>
    public static IServiceCollection AddCoreApiCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redis = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()?.ConnectionString;
        if (!string.IsNullOrWhiteSpace(redis))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redis;
                options.InstanceName = CacheKeys.InstanceName;
            });
        }

        services.AddHybridCache();
        services.AddSingleton<CacheMetrics>();
        services.AddScoped<ReferenceDataReader>();
        services.AddScoped<IReferenceDataReader, CachedReferenceDataReader>();
        services.AddScoped<RecipeCalculations>();
        services.AddScoped<IRecipeCalculations, CachedRecipeCalculations>();
        return services;
    }

    /// <summary>After a successful write request (not GET/HEAD/OPTIONS) removes the caller's cached calculations.</summary>
    public static IApplicationBuilder UseCalculationCacheInvalidation(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            await next(context);
            var method = context.Request.Method;
            if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method)
                || context.Response.StatusCode >= StatusCodes.Status400BadRequest)
            {
                return;
            }

            var currentUser = context.RequestServices.GetRequiredService<ICurrentUser>();
            if (currentUser.IsAuthenticated)
            {
                var cache = context.RequestServices.GetRequiredService<HybridCache>();
                await cache.RemoveByTagAsync(CacheKeys.UserCalculationsTag(currentUser.Id), context.RequestAborted);
            }
        });
}
