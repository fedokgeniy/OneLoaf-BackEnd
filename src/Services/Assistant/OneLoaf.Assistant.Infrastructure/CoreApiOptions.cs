using System.ComponentModel.DataAnnotations;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>Core API settings of the assistant.</summary>
public sealed class CoreApiOptions
{
    /// <summary>Configuration section name (<c>Assistant__CoreApiBaseUrl</c>).</summary>
    public const string SectionName = "Assistant";

    /// <summary>Internal URL of the Core API as seen from the assistant container.</summary>
    [Required]
    public string CoreApiBaseUrl { get; set; } = string.Empty;
}
