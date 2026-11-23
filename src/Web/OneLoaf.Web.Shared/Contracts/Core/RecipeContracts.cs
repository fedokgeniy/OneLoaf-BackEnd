namespace OneLoaf.Web.Shared.Contracts.Core;

/// <summary>Recipe in a list (<c>GET /api/v1/recipes</c>).</summary>
public sealed record RecipeSummaryDto(
    Guid Id,
    string Title,
    Guid? ParentRecipeId,
    int YieldLoaves,
    bool IsArchived,
    DateTimeOffset UpdatedAt);

/// <summary>A recipe with its ingredient lines and stages (<c>GET /api/v1/recipes/{id}</c>).</summary>
public sealed record RecipeDto(
    Guid Id,
    Guid? ParentRecipeId,
    string Title,
    string? Description,
    int YieldLoaves,
    decimal? LoafWeightG,
    string? Notes,
    bool IsArchived,
    IReadOnlyList<RecipeIngredientDto> Ingredients,
    IReadOnlyList<RecipeStageDto> Stages,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Ingredient line of a recipe; <see cref="IsInherited"/> marks a line copied from the parent of a variant.</summary>
public sealed record RecipeIngredientDto(
    Guid LineId,
    Guid IngredientId,
    string IngredientName,
    string CategoryCode,
    string UnitCode,
    decimal Quantity,
    bool IsOptional,
    int SortOrder,
    string? Note,
    decimal? ProteinPercent,
    bool IsInherited);

/// <summary>Stage of a recipe. <see cref="ProofMode"/> is <c>warm</c>, <c>cold</c> or null.</summary>
public sealed record RecipeStageDto(
    Guid LineId,
    int SortOrder,
    string Name,
    string StageTypeCode,
    int MinDurationMinutes,
    int MaxDurationMinutes,
    int ActiveMinutes,
    string? ProofMode,
    decimal? TemperatureC,
    bool UsesOven,
    bool UsesMixer,
    bool IsOptional,
    string? Instructions,
    bool IsInherited);

/// <summary>Ingredient quantities for a number of loaves (<c>GET /recipes/{id}/scaled</c>, US-06).</summary>
public sealed record ScaledRecipeDto(Guid RecipeId, int YieldLoaves, int Loaves, IReadOnlyList<ScaledIngredientDto> Ingredients);

/// <summary>One scaled ingredient line.</summary>
public sealed record ScaledIngredientDto(
    Guid LineId, Guid IngredientId, string IngredientName, string UnitCode, decimal Quantity, bool IsOptional);

/// <summary>Cost of a batch (<c>GET /recipes/{id}/cost</c>, US-14, US-15, US-47–49). <see cref="Total"/> is null when incomplete.</summary>
public sealed record CostBreakdownDto(
    Guid RecipeId,
    int Loaves,
    DateOnly AsOf,
    string Currency,
    bool IsComplete,
    IReadOnlyList<CostLineDto> Lines,
    decimal? Total,
    IReadOnlyList<MissingPriceDto> MissingPrices);

/// <summary>
/// Cost line. <see cref="Type"/>: <c>ingredient</c>, <c>electricity</c>, <c>packaging</c>, <c>labor</c>, <c>loss</c>,
/// <c>total</c> (see <see cref="CostLineTypes"/>).
/// </summary>
public sealed record CostLineDto(
    int LineNo,
    string Type,
    Guid? IngredientId,
    string Description,
    decimal? Quantity,
    string? Unit,
    decimal? UnitPrice,
    decimal? Amount,
    bool IsComplete);

/// <summary>Known cost line types; other values are shown as ordinary lines.</summary>
public static class CostLineTypes
{
    /// <summary>The closing total line.</summary>
    public const string Total = "total";
}

/// <summary>An ingredient without a price on the cost date.</summary>
public sealed record MissingPriceDto(Guid IngredientId, string Name);

/// <summary>Dough weight, kneading cycles and starter amount (<c>GET /recipes/{id}/production-figures</c>, US-17, US-18).</summary>
public sealed record ProductionFiguresDto(
    Guid RecipeId, int Loaves, decimal DoughWeightG, int? KneadingCycles, decimal StarterAmountG);

/// <summary>Planned stage times from a start (<c>GET /recipes/{id}/timeline</c>, US-16).</summary>
public sealed record TimelineDto(
    Guid RecipeId,
    DateTimeOffset Start,
    IReadOnlyList<TimelineStageDto> Stages,
    DateTimeOffset ReadyFrom,
    DateTimeOffset ReadyTo);

/// <summary>One planned stage.</summary>
public sealed record TimelineStageDto(
    Guid LineId,
    int SortOrder,
    string Name,
    string StageTypeCode,
    int MinMinutes,
    int MaxMinutes,
    DateTimeOffset PlannedStartAt,
    DateTimeOffset PlannedEndMinAt,
    DateTimeOffset PlannedEndMaxAt);

/// <summary>When to start so the recipe is ready at <see cref="ReadyAt"/> (<c>GET /recipes/{id}/start-window</c>, US-52).</summary>
public sealed record StartWindowDto(Guid RecipeId, DateTimeOffset ReadyAt, DateTimeOffset StartFrom, DateTimeOffset StartTo);
