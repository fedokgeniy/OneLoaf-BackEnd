using System.Globalization;
using Microsoft.Extensions.Caching.Hybrid;

namespace OneLoaf.CoreApi.Infrastructure.Caching;

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

    /// <summary>Tag of all catalog pages.</summary>
    public const string LibraryTag = "library";

    /// <summary>A catalog page.</summary>
    public static string LibraryPage(string search, int page, int pageSize) =>
        string.Create(CultureInfo.InvariantCulture, $"library:page:{search.ToLowerInvariant()}:{page}:{pageSize}");

    /// <summary>Tag of all calculation results of a user.</summary>
    public static string UserCalculationsTag(Guid userId) => $"user:{userId:N}:calc";
}

/// <summary>Lifetimes of the cached entries.</summary>
public static class CacheLifetimes
{
    /// <summary>Reference tables change only with a migration.</summary>
    public static readonly HybridCacheEntryOptions Reference = new() { Expiration = TimeSpan.FromHours(24), LocalCacheExpiration = TimeSpan.FromHours(1) };

    /// <summary>Catalog pages; also removed by tag on publish, unpublish and removal.</summary>
    public static readonly HybridCacheEntryOptions Library = new() { Expiration = TimeSpan.FromMinutes(5), LocalCacheExpiration = TimeSpan.FromMinutes(1) };

    /// <summary>Calculation results; also removed by tag on any change of the user's data.</summary>
    public static readonly HybridCacheEntryOptions Calculation = new() { Expiration = TimeSpan.FromMinutes(10), LocalCacheExpiration = TimeSpan.FromMinutes(2) };
}
