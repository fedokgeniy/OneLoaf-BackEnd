namespace OneLoaf.Domain.Entities;

/// <summary>
/// E2, US-05 - US-09. A recipe or a variant of another recipe.
/// A variant (ParentRecipeId != null) stores only differences:
///  - changed scalar fields in <see cref="VariantOverrides"/> (JSON);
///  - changed ingredients and stages as rows that replace, remove or add parent rows
///    (see <see cref="RecipeIngredient.ReplacesItemId"/>, <see cref="RecipeStage.ReplacesStageId"/>).
/// </summary>
public class Recipe
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public Guid? ParentRecipeId { get; set; }
    /// <summary>Set when the recipe was copied from the public library (US-42, attribution).</summary>
    public Guid? CopiedFromPublishedRecipeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    /// <summary>Number of loaves the ingredient quantities are written for (scaling base, US-06).</summary>
    public int YieldLoaves { get; set; } = 1;
    public decimal? LoafWeightG { get; set; }
    /// <summary>JSON object with overridden scalar fields of a variant, e.g. {"title":"...","loafWeightG":900}. Null for base recipes.</summary>
    public string? VariantOverrides { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public User Owner { get; set; } = null!;
    public Recipe? ParentRecipe { get; set; }
    public ICollection<Recipe> Variants { get; set; } = new List<Recipe>();
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStage> Stages { get; set; } = new List<RecipeStage>();
}

/// <summary>E2, US-05, US-07, US-09. Ingredient line of a recipe.</summary>
public class RecipeIngredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid IngredientId { get; set; }
    /// <summary>Quantity in the ingredient's unit for Recipe.YieldLoaves loaves.</summary>
    public decimal Quantity { get; set; }
    public bool IsOptional { get; set; }
    public int SortOrder { get; set; }
    public string? Note { get; set; }
    /// <summary>Variant only: the parent line that this line replaces (or removes, if IsRemoved).</summary>
    public Guid? ReplacesItemId { get; set; }
    public bool IsRemoved { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
    public RecipeIngredient? ReplacesItem { get; set; }
}

/// <summary>E2, US-05, US-09, US-16, US-49. Ordered stage of a recipe with a duration range.</summary>
public class RecipeStage
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int SortOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Code from stage_types.</summary>
    public string StageTypeCode { get; set; } = string.Empty;
    public int MinDurationMinutes { get; set; }
    public int MaxDurationMinutes { get; set; }
    /// <summary>Hands-on time of the baker, for the labor cost line (US-49).</summary>
    public int ActiveMinutes { get; set; }
    public ProofMode? ProofMode { get; set; }
    public decimal? TemperatureC { get; set; }
    public bool UsesOven { get; set; }
    public bool UsesMixer { get; set; }
    public bool IsOptional { get; set; }
    public string? Instructions { get; set; }
    public Guid? ReplacesStageId { get; set; }
    public bool IsRemoved { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public RecipeStage? ReplacesStage { get; set; }
}
