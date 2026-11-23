using System.ComponentModel.DataAnnotations;

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
