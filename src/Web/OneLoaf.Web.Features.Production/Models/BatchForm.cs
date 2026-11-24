using System.ComponentModel.DataAnnotations;
using System.Globalization;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Production.Models;

/// <summary>How the start time is chosen (US-52).</summary>
public enum PlanMode
{
    /// <summary>The baker picks the start.</summary>
    StartAt,

    /// <summary>The baker picks when the bread must be ready; the start comes from the recipes' start windows.</summary>
    ReadyBy,
}

/// <summary>New batch form (US-20, US-29, US-52) with the limits of the batch validator.</summary>
public sealed class BatchForm
{
    /// <summary>Batch name.</summary>
    [Required(ErrorMessage = "Enter a name.")]
    [StringLength(200, ErrorMessage = "Use at most 200 characters.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Enter a name.")]
    public string? Name { get; set; }

    /// <summary>Start time or ready-by time.</summary>
    public PlanMode Mode { get; set; } = PlanMode.StartAt;

    /// <summary>Date (yyyy-MM-dd) of the start or of "ready by".</summary>
    [Required(ErrorMessage = "Enter the date.")]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Enter a date.")]
    public string? Date { get; set; }

    /// <summary>Time (HH:mm).</summary>
    [Required(ErrorMessage = "Enter the time.")]
    [RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "Enter a time.")]
    public string? Time { get; set; }

    /// <summary>Notes.</summary>
    [StringLength(4000, ErrorMessage = "Use at most 4000 characters.")]
    public string? Notes { get; set; }

    /// <summary>Recipes of the batch.</summary>
    public List<BatchItemForm> Items { get; set; } = [];

    /// <summary>The chosen moment in the browser's time zone, or null while the fields are incomplete.</summary>
    public DateTimeOffset? Moment()
    {
        if (!DateTime.TryParseExact($"{Date} {Time}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    /// <summary>
    /// Share of the recipe quantities to bake with, offered by the stock check of the preview; null = the full recipe.
    /// </summary>
    public decimal? IngredientScale { get; set; }

    /// <summary>True when the form can be previewed: a moment and at least one complete item.</summary>
    public bool CanPreview => Moment() is not null && Items.Count > 0 && Items.All(i => i.IsComplete);

    /// <summary>The request for <paramref name="plannedStartAt"/>.</summary>
    public BatchRequest ToRequest(DateTimeOffset plannedStartAt) => new(
        string.IsNullOrWhiteSpace(Name) ? "New batch" : Name.Trim(),
        plannedStartAt,
        [.. Items.Select(i => i.ToRequest())],
        null,
        string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
        "manual",
        IngredientScale);
}

/// <summary>One recipe of the new batch.</summary>
public sealed class BatchItemForm
{
    /// <summary>Recipe id.</summary>
    [Required(ErrorMessage = "Choose a recipe.")]
    public string? RecipeId { get; set; }

    /// <summary>Loaves.</summary>
    [Range(1, 1000, ErrorMessage = "Enter 1 to 1000 loaves.")]
    public int Loaves { get; set; } = 1;

    /// <summary>Starter used; null for none.</summary>
    public string? StarterId { get; set; }

    /// <summary>Remind to feed the starter early enough for kneading (US-29); the API knows how early that is.</summary>
    public bool Remind { get; set; } = true;

    /// <summary>Recipe chosen and loaves valid.</summary>
    public bool IsComplete => Guid.TryParse(RecipeId, out _) && Loaves is >= 1 and <= 1000;

    /// <summary>The request item; a reminder only with a starter.</summary>
    public BatchItemRequest ToRequest()
    {
        Guid? starter = Guid.TryParse(StarterId, CultureInfo.InvariantCulture, out var id) ? id : null;
        return new BatchItemRequest(Guid.Parse(RecipeId!, CultureInfo.InvariantCulture), Loaves, starter,
            starter is not null && Remind);
    }
}
