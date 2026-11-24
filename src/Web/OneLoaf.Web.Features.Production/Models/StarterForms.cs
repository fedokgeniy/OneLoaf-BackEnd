using System.ComponentModel.DataAnnotations;
using System.Globalization;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Production.Models;

/// <summary>Starter form (US-27, US-30).</summary>
public sealed class StarterForm
{
    /// <summary>Name.</summary>
    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(100, ErrorMessage = "Use at most 100 characters.")]
    public string? Name { get; set; }

    /// <summary>Hydration in percent (water per flour).</summary>
    [Required(ErrorMessage = "Enter the hydration.")]
    [Range(typeof(decimal), "10", "500", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 10 to 500 %.")]
    public decimal? HydrationPercent { get; set; } = 100m;

    /// <summary>regular, weekly or fridge.</summary>
    [Required(ErrorMessage = "Choose how you feed it.")]
    public string? FeedingMode { get; set; } = "regular";

    /// <summary>Hours between feedings.</summary>
    [Required(ErrorMessage = "Enter the interval.")]
    [Range(1, 720, ErrorMessage = "Enter 1 to 720 hours.")]
    public int? FeedingIntervalHours { get; set; } = 24;

    /// <summary>Flour the starter is fed with: an ingredient of the catalog; feedings take from its stock.</summary>
    public string? FlourIngredientId { get; set; }

    /// <summary>Share of the fed starter that can go into dough once it is ripe, in percent.</summary>
    [Required(ErrorMessage = "Enter the usable share.")]
    [Range(typeof(decimal), "5", "100", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 5 to 100 %.")]
    public decimal? UsablePercent { get; set; } = 66.7m;

    /// <summary>Ingredient whose stock a feeding takes the water from.</summary>
    public string? WaterIngredientId { get; set; }

    /// <summary>Hours from feeding to peak (US-30); the starter cannot be used before it.</summary>
    [Required(ErrorMessage = "Enter the time from a feeding to the peak.")]
    [Range(typeof(decimal), "0.5", "72", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0.5 to 72 hours.")]
    public decimal? RiseHours { get; set; }

    /// <summary>Active starters get feeding reminders.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Notes.</summary>
    [StringLength(2000, ErrorMessage = "Use at most 2000 characters.")]
    public string? Notes { get; set; }

    /// <summary>From an existing starter.</summary>
    public static StarterForm From(StarterDto starter)
    {
        ArgumentNullException.ThrowIfNull(starter);
        return new StarterForm
        {
            Name = starter.Name,
            HydrationPercent = starter.HydrationPercent,
            FeedingMode = starter.FeedingMode,
            FeedingIntervalHours = starter.FeedingIntervalHours,
            FlourIngredientId = starter.FlourIngredientId?.ToString(),
            UsablePercent = Math.Round(starter.UsableShare * 100m, 1),
            WaterIngredientId = starter.WaterIngredientId?.ToString(),
            RiseHours = starter.RiseHours,
            IsActive = starter.IsActive,
            Notes = starter.Notes,
        };
    }

    /// <summary>The request.</summary>
    public StarterRequest ToRequest() => new(
        Name!.Trim(), HydrationPercent!.Value, FeedingMode!, FeedingIntervalHours!.Value,
        null,
        RiseHours, IsActive, string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        Guid.TryParse(FlourIngredientId, CultureInfo.InvariantCulture, out var flourIngredient) ? flourIngredient : null,
        Guid.TryParse(WaterIngredientId, CultureInfo.InvariantCulture, out var waterIngredient) ? waterIngredient : null,
        UsablePercent!.Value / 100m);
}

/// <summary>Feeding form (US-28); "starter kept" lives here, not on the starter (mockup-vs-api).</summary>
public sealed class FeedingForm
{
    /// <summary>When (yyyy-MM-ddTHH:mm, local).</summary>
    [Required(ErrorMessage = "Enter when you fed it.")]
    public string? FedAt { get; set; }

    /// <summary>Flour in grams.</summary>
    [Required(ErrorMessage = "Enter the flour.")]
    [Range(typeof(decimal), "0", "100000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0 g or more.")]
    public decimal? FlourG { get; set; }

    /// <summary>Water in grams.</summary>
    [Required(ErrorMessage = "Enter the water.")]
    [Range(typeof(decimal), "0", "100000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0 g or more.")]
    public decimal? WaterG { get; set; }

    /// <summary>Flour ingredient of this feeding (id as text); the usual flour of the starter by default.</summary>
    public string? FlourIngredientId { get; set; }

    /// <summary>Water ingredient of this feeding (id as text); the usual water of the starter by default.</summary>
    public string? WaterIngredientId { get; set; }

    /// <summary>Starter kept before feeding, in grams.</summary>
    [Range(typeof(decimal), "0", "100000", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter 0 g or more.")]
    public decimal? StarterKeptG { get; set; }

    /// <summary>Room or dough temperature.</summary>
    [Range(typeof(decimal), "-30", "60", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter −30 to 60 °C.")]
    public decimal? TemperatureC { get; set; }

    /// <summary>Note.</summary>
    [StringLength(500, ErrorMessage = "Use at most 500 characters.")]
    public string? Note { get; set; }

    /// <summary>The request; null while the date is invalid.</summary>
    public FeedingRequest? ToRequest()
    {
        if (!DateTime.TryParseExact(FedAt, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        return new FeedingRequest(new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)), FlourG!.Value, WaterG!.Value,
            StarterKeptG, TemperatureC, string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(),
            Guid.TryParse(FlourIngredientId, out var flour) ? flour : null,
            Guid.TryParse(WaterIngredientId, out var water) ? water : null);
    }
}
