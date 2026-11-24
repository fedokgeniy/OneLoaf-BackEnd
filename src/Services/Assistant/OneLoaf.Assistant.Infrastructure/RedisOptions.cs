using System.ComponentModel.DataAnnotations;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>Redis settings (<c>Redis__ConnectionString</c>).</summary>
public sealed class RedisOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Redis";

    /// <summary>StackExchange.Redis connection string.</summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;
}
