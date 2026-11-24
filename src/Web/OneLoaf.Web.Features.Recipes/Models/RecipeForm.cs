using System.ComponentModel.DataAnnotations;
using System.Globalization;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Recipes.Models;

/// <summary>Editable recipe (US-05) with the limits of the Core API validator; lists keep the request order.</summary>
public sealed class RecipeForm : IValidatableObject
{
    /// <summary>Title.</summary>
    [Required(ErrorMessage = "Enter a title.")]
    [StringLength(200, ErrorMessage = "Use at most 200 characters.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Enter a title.")]
    public string? Title { get; set; }

    /// <summary>Short description.</summary>
    [StringLength(4000, ErrorMessage = "Use at most 4000 characters.")]
    public string? Description { get; set; }

    /// <summary>Loaves the quantities are written for.</summary>
    [Range(1, 1000, ErrorMessage = "Enter 1 to 1000 loaves.")]
    public int YieldLoaves { get; set; } = 1;

    /// <summary>Dough weight of one loaf in grams.</summary>
    [Range(typeof(decimal), "0.1", "9999999", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter a weight above 0 g.")]
    public decimal? LoafWeightG { get; set; }

    /// <summary>Notes.</summary>
    [StringLength(4000, ErrorMessage = "Use at most 4000 characters.")]
    public string? Notes { get; set; }

    /// <summary>Ingredient lines in order.</summary>
    public List<IngredientLineForm> Ingredients { get; set; } = [];

    /// <summary>Stages in order.</summary>
    public List<StageLineForm> Stages { get; set; } = [];

    /// <summary>A new recipe with one empty ingredient line and one stage to start from.</summary>
    public static RecipeForm New() => new() { Ingredients = [new()], Stages = [new()] };

    /// <summary>The form of an existing recipe.</summary>
    public static RecipeForm From(RecipeDto recipe)
    {
        ArgumentNullException.ThrowIfNull(recipe);
        return new RecipeForm
        {
            Title = recipe.Title,
            Description = recipe.Description,
            YieldLoaves = recipe.YieldLoaves,
            LoafWeightG = recipe.LoafWeightG,
            Notes = recipe.Notes,
            Ingredients = [.. recipe.Ingredients.OrderBy(i => i.SortOrder).Select(IngredientLineForm.From)],
            Stages = [.. recipe.Stages.OrderBy(s => s.SortOrder).Select(StageLineForm.From)],
        };
    }

    /// <summary>The request; sort orders follow the list order.</summary>
    public RecipeRequest ToRequest() => new(
        Title!.Trim(),
        Blank(Description),
        YieldLoaves,
        LoafWeightG,
        Blank(Notes),
        [.. Ingredients.Select((line, i) => line.ToInput(i + 1))],
        [.. Stages.Select((stage, i) => stage.ToInput(i + 1))]);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Ingredients.Count > 200 || Stages.Count > 200)
        {
            yield return new ValidationResult("A recipe has at most 200 ingredient lines and 200 stages.", [nameof(Ingredients)]);
        }
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Editable ingredient line.</summary>
public sealed class IngredientLineForm
{
    /// <summary>Line id of an existing line; null for a new one.</summary>
    public Guid? LineId { get; set; }

    /// <summary>Ingredient id as the select value.</summary>
    [Required(ErrorMessage = "Choose an ingredient.")]
    public string? IngredientId { get; set; }

    /// <summary>Quantity in the ingredient's unit.</summary>
    [Required(ErrorMessage = "Enter an amount.")]
    [Range(typeof(decimal), "0", "999999998", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter an amount of 0 or more.")]
    public decimal? Quantity { get; set; }

    /// <summary>Optional ingredient.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Note.</summary>
    [StringLength(500, ErrorMessage = "Use at most 500 characters.")]
    public string? Note { get; set; }

    /// <summary>The line is inherited from the parent of a variant (shown, not editable differently).</summary>
    public bool IsInherited { get; set; }

    /// <summary>From an API line.</summary>
    public static IngredientLineForm From(RecipeIngredientDto line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new IngredientLineForm
        {
            LineId = line.LineId,
            IngredientId = line.IngredientId.ToString(),
            Quantity = line.Quantity,
            IsOptional = line.IsOptional,
            Note = line.Note,
            IsInherited = line.IsInherited,
        };
    }

    /// <summary>To the request line.</summary>
    public RecipeIngredientInput ToInput(int sortOrder) => new(
        LineId, Guid.Parse(IngredientId!, CultureInfo.InvariantCulture), Quantity ?? 0m, IsOptional,
        string.IsNullOrWhiteSpace(Note) ? null : Note.Trim(), sortOrder);
}

/// <summary>Editable stage (US-05, US-09, US-49).</summary>
public sealed class StageLineForm : IValidatableObject
{
    /// <summary>Line id of an existing stage; null for a new one.</summary>
    public Guid? LineId { get; set; }

    /// <summary>Name, e.g. "Bulk fermentation".</summary>
    [Required(ErrorMessage = "Enter a stage name.")]
    [StringLength(100, ErrorMessage = "Use at most 100 characters.")]
    public string? Name { get; set; }

    /// <summary>Stage type code from <c>/reference/stage-types</c>.</summary>
    [Required(ErrorMessage = "Choose a stage type.")]
    public string? StageTypeCode { get; set; }

    /// <summary>Shortest duration.</summary>
    [Required(ErrorMessage = "Enter the minimum.")]
    [Range(0, 10080, ErrorMessage = "Enter 0 to 10 080 minutes.")]
    public int? MinDurationMinutes { get; set; }

    /// <summary>Longest duration.</summary>
    [Required(ErrorMessage = "Enter the maximum.")]
    [Range(0, 10080, ErrorMessage = "Enter 0 to 10 080 minutes.")]
    public int? MaxDurationMinutes { get; set; }

    /// <summary>Hands-on minutes for the labor cost (US-49).</summary>
    [Range(0, 10080, ErrorMessage = "Enter 0 to 10 080 minutes.")]
    public int ActiveMinutes { get; set; }

    /// <summary><c>warm</c>, <c>cold</c> or null.</summary>
    public string? ProofMode { get; set; }

    /// <summary>Temperature in °C.</summary>
    [Range(typeof(decimal), "-30", "350", ParseLimitsInInvariantCulture = true, ErrorMessage = "Enter −30 to 350 °C.")]
    public decimal? TemperatureC { get; set; }

    /// <summary>Uses the oven (electricity cost).</summary>
    public bool UsesOven { get; set; }

    /// <summary>Uses the mixer (kneading cycles).</summary>
    public bool UsesMixer { get; set; }

    /// <summary>Optional stage.</summary>
    public bool IsOptional { get; set; }

    /// <summary>Instructions.</summary>
    [StringLength(4000, ErrorMessage = "Use at most 4000 characters.")]
    public string? Instructions { get; set; }

    /// <summary>A new stage with the same settings: it is a line of its own, so it has no line id.</summary>
    public StageLineForm Copy() => new()
    {
        Name = Name,
        StageTypeCode = StageTypeCode,
        MinDurationMinutes = MinDurationMinutes,
        MaxDurationMinutes = MaxDurationMinutes,
        ActiveMinutes = ActiveMinutes,
        ProofMode = ProofMode,
        TemperatureC = TemperatureC,
        UsesOven = UsesOven,
        UsesMixer = UsesMixer,
        IsOptional = IsOptional,
        Instructions = Instructions,
    };

    /// <summary>Inherited from the parent of a variant.</summary>
    public bool IsInherited { get; set; }

    /// <summary>The extra fields are shown.</summary>
    public bool Expanded { get; set; }

    /// <summary>From an API stage.</summary>
    public static StageLineForm From(RecipeStageDto stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        return new StageLineForm
        {
            LineId = stage.LineId,
            Name = stage.Name,
            StageTypeCode = stage.StageTypeCode,
            MinDurationMinutes = stage.MinDurationMinutes,
            MaxDurationMinutes = stage.MaxDurationMinutes,
            ActiveMinutes = stage.ActiveMinutes,
            ProofMode = stage.ProofMode,
            TemperatureC = stage.TemperatureC,
            UsesOven = stage.UsesOven,
            UsesMixer = stage.UsesMixer,
            IsOptional = stage.IsOptional,
            Instructions = stage.Instructions,
            IsInherited = stage.IsInherited,
        };
    }

    /// <summary>To the request stage.</summary>
    public RecipeStageInput ToInput(int sortOrder) => new(
        LineId, Name!.Trim(), StageTypeCode!, MinDurationMinutes ?? 0, MaxDurationMinutes ?? 0, ActiveMinutes,
        ProofMode, TemperatureC, UsesOven, UsesMixer, IsOptional,
        string.IsNullOrWhiteSpace(Instructions) ? null : Instructions.Trim(), sortOrder);

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinDurationMinutes is { } min && MaxDurationMinutes is { } max && max < min)
        {
            yield return new ValidationResult("The maximum must not be shorter than the minimum.", [nameof(MaxDurationMinutes)]);
        }

        if (MaxDurationMinutes is { } longest && ActiveMinutes > longest)
        {
            yield return new ValidationResult("Hands-on time cannot exceed the maximum.", [nameof(ActiveMinutes)]);
        }
    }
}

/// <summary>Title of a new variant (US-07).</summary>
public sealed class VariantForm
{
    /// <summary>Title.</summary>
    [Required(ErrorMessage = "Enter a title.")]
    [StringLength(200, ErrorMessage = "Use at most 200 characters.")]
    [RegularExpression(@".*\S.*", ErrorMessage = "Enter a title.")]
    public string? Title { get; set; }
}
