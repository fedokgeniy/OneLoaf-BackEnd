using System.Diagnostics.Metrics;
using Microsoft.Extensions.Caching.Hybrid;
using OneLoaf.ServiceDefaults.Constants;

namespace OneLoaf.CoreApi.Infrastructure.Caching;

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
