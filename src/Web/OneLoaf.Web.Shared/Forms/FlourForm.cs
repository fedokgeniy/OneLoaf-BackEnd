using System.ComponentModel.DataAnnotations;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Shared.Forms;

/// <summary>Flour of the catalog (US-11) with the limits of the catalog validator.</summary>
public sealed class FlourForm
{
    /// <summary>Name, unique within the user.</summary>
    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(200, ErrorMessage = "Use at most 200 characters.")]
    public string? Name { get; set; }

    /// <summary>Grain type code.</summary>
    [Required(ErrorMessage = "Choose a flour type.")]
    public string? FlourTypeCode { get; set; }

    /// <summary>Protein, %.</summary>
    [Range(typeof(decimal), "0", "100", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter a percentage from 0 to 100.")]
    public decimal? ProteinPercent { get; set; }

    /// <summary>Grind, for example fine or stone-ground.</summary>
    [StringLength(50, ErrorMessage = "Use at most 50 characters.")]
    public string? Grind { get; set; }

    /// <summary>Notes.</summary>
    [StringLength(2000, ErrorMessage = "Use at most 2000 characters.")]
    public string? Notes { get; set; }

    /// <summary>From an existing flour.</summary>
    public static FlourForm From(FlourDto flour)
    {
        ArgumentNullException.ThrowIfNull(flour);
        return new FlourForm
        {
            Name = flour.Name,
            FlourTypeCode = flour.FlourTypeCode,
            ProteinPercent = flour.ProteinPercent,
            Grind = flour.Grind,
            Notes = flour.Notes,
        };
    }

    /// <summary>The request.</summary>
    public FlourRequest ToRequest() => new(
        Name!.Trim(), FlourTypeCode!, ProteinPercent,
        string.IsNullOrWhiteSpace(Grind) ? null : Grind.Trim(),
        string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim());
}
