namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary><c>POST /recipes</c>, <c>PUT /recipes/{id}</c> body (US-05). Lines with a <c>LineId</c> are updated, others added.</summary>
public sealed record RecipeRequest(
    string Title,
    string? Description,
    int YieldLoaves,
    decimal? LoafWeightG,
    string? Notes,
    IReadOnlyList<RecipeIngredientInput> Ingredients,
    IReadOnlyList<RecipeStageInput> Stages);

/// <summary>Ingredient line of a <see cref="RecipeRequest"/>.</summary>
public sealed record RecipeIngredientInput(
    Guid? LineId, Guid IngredientId, decimal Quantity, bool IsOptional, string? Note, int? SortOrder);

/// <summary>Stage of a <see cref="RecipeRequest"/>; <see cref="ProofMode"/> is <c>warm</c>, <c>cold</c> or null.</summary>
public sealed record RecipeStageInput(
    Guid? LineId,
    string Name,
    string StageTypeCode,
    int MinDurationMinutes,
    int MaxDurationMinutes,
    int ActiveMinutes,
    string? ProofMode,
    decimal? TemperatureC,
    bool? UsesOven,
    bool? UsesMixer,
    bool IsOptional,
    string? Instructions,
    int? SortOrder);

/// <summary><c>POST /recipes/{id}/variants</c> body (US-07).</summary>
public sealed record CreateVariantRequest(string Title);

/// <summary>Variant compared with its parent (<c>GET /recipes/{id}/comparison</c>, US-08).</summary>
public sealed record RecipeComparisonDto(
    Guid RecipeId,
    Guid ParentRecipeId,
    IReadOnlyList<string> ChangedFields,
    IReadOnlyList<IngredientComparisonDto> Ingredients,
    IReadOnlyList<StageComparisonDto> Stages);

/// <summary>One ingredient line of a comparison; <see cref="Change"/> is unchanged, changed, added or removed.</summary>
public sealed record IngredientComparisonDto(string Change, RecipeIngredientDto? Parent, RecipeIngredientDto? Variant);

/// <summary>One stage of a comparison.</summary>
public sealed record StageComparisonDto(string Change, RecipeStageDto? Parent, RecipeStageDto? Variant);
