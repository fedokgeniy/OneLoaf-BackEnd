using OneLoaf.CoreApi.Application.Common;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.Application.Recipes;

/// <summary>Recipe in a list.</summary>
/// <param name="Id">Id.</param>
/// <param name="Title">Title.</param>
/// <param name="ParentRecipeId">Parent of a variant.</param>
/// <param name="YieldLoaves">Loaves the quantities are written for.</param>
/// <param name="IsArchived">Hidden from the default list.</param>
/// <param name="UpdatedAt">Last change.</param>
public sealed record RecipeSummaryDto(
    Guid Id, string Title, Guid? ParentRecipeId, int YieldLoaves, bool IsArchived, DateTimeOffset UpdatedAt);

/// <summary>Effective ingredient line: a variant shows inherited lines with <see cref="IsInherited"/> = true.</summary>
/// <param name="LineId">Id of the row that defines the line (send it back on update).</param>
/// <param name="IngredientId">Ingredient.</param>
/// <param name="IngredientName">Ingredient name.</param>
/// <param name="CategoryCode">Ingredient category.</param>
/// <param name="UnitCode">Unit of the quantity.</param>
/// <param name="Quantity">Quantity for the recipe yield.</param>
/// <param name="IsOptional">Not counted in cost and dough weight.</param>
/// <param name="SortOrder">Order.</param>
/// <param name="Note">Note.</param>
/// <param name="ProteinPercent">Protein of the linked flour.</param>
/// <param name="IsInherited">Comes from a parent recipe.</param>
public sealed record RecipeIngredientDto(
    Guid LineId, Guid IngredientId, string IngredientName, string CategoryCode, string UnitCode, decimal Quantity,
    bool IsOptional, int SortOrder, string? Note, decimal? ProteinPercent, bool IsInherited);

/// <summary>Effective stage.</summary>
/// <param name="LineId">Id of the row that defines the stage.</param>
/// <param name="SortOrder">Order.</param>
/// <param name="Name">Name.</param>
/// <param name="StageTypeCode">Stage type.</param>
/// <param name="MinDurationMinutes">Minimum duration.</param>
/// <param name="MaxDurationMinutes">Maximum duration.</param>
/// <param name="ActiveMinutes">Hands-on time.</param>
/// <param name="ProofMode">warm or cold (proof stages).</param>
/// <param name="TemperatureC">Temperature.</param>
/// <param name="UsesOven">Uses the oven.</param>
/// <param name="UsesMixer">Uses the mixer.</param>
/// <param name="IsOptional">Optional stage.</param>
/// <param name="Instructions">Instructions.</param>
/// <param name="IsInherited">Comes from a parent recipe.</param>
public sealed record RecipeStageDto(
    Guid LineId, int SortOrder, string Name, string StageTypeCode, int MinDurationMinutes, int MaxDurationMinutes,
    int ActiveMinutes, ProofMode? ProofMode, decimal? TemperatureC, bool UsesOven, bool UsesMixer, bool IsOptional,
    string? Instructions, bool IsInherited);

/// <summary>Effective recipe (US-05, US-07): a variant is shown with the inherited content resolved.</summary>
/// <param name="Id">Id.</param>
/// <param name="ParentRecipeId">Parent of a variant.</param>
/// <param name="Title">Title.</param>
/// <param name="Description">Description.</param>
/// <param name="YieldLoaves">Loaves the quantities are written for.</param>
/// <param name="LoafWeightG">Weight of one loaf, g.</param>
/// <param name="Notes">Notes.</param>
/// <param name="IsArchived">Archived.</param>
/// <param name="Ingredients">Effective ingredient lines.</param>
/// <param name="Stages">Effective stages.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="UpdatedAt">Last change.</param>
public sealed record RecipeDto(
    Guid Id, Guid? ParentRecipeId, string Title, string? Description, int YieldLoaves, decimal? LoafWeightG,
    string? Notes, bool IsArchived, IReadOnlyList<RecipeIngredientDto> Ingredients, IReadOnlyList<RecipeStageDto> Stages,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>Ingredient line in a create/update request.</summary>
/// <param name="LineId">Id of an existing line (from GET) to keep it; null for a new line.</param>
/// <param name="IngredientId">Ingredient of the user's catalog.</param>
/// <param name="Quantity">Quantity for the recipe yield.</param>
/// <param name="IsOptional">Optional ingredient.</param>
/// <param name="Note">Note.</param>
/// <param name="SortOrder">Order; defaults to the position in the list.</param>
public sealed record RecipeIngredientInput(
    Guid? LineId, Guid IngredientId, decimal Quantity, bool IsOptional = false, string? Note = null, int? SortOrder = null);

/// <summary>Stage in a create/update request.</summary>
/// <param name="LineId">Id of an existing stage (from GET) to keep it; null for a new stage.</param>
/// <param name="Name">Name.</param>
/// <param name="StageTypeCode">Stage type.</param>
/// <param name="MinDurationMinutes">Minimum duration.</param>
/// <param name="MaxDurationMinutes">Maximum duration.</param>
/// <param name="ActiveMinutes">Hands-on time, not more than the maximum.</param>
/// <param name="ProofMode">warm or cold, fermentation stages only.</param>
/// <param name="TemperatureC">Temperature, -30..350.</param>
/// <param name="UsesOven">Uses the oven; defaults to the stage type.</param>
/// <param name="UsesMixer">Uses the mixer; defaults to the stage type.</param>
/// <param name="IsOptional">Optional stage.</param>
/// <param name="Instructions">Instructions.</param>
/// <param name="SortOrder">Order; defaults to the position in the list.</param>
public sealed record RecipeStageInput(
    Guid? LineId, string Name, string StageTypeCode, int MinDurationMinutes, int MaxDurationMinutes, int ActiveMinutes = 0,
    ProofMode? ProofMode = null, decimal? TemperatureC = null, bool? UsesOven = null, bool? UsesMixer = null,
    bool IsOptional = false, string? Instructions = null, int? SortOrder = null);

/// <summary>Body of <c>POST /recipes</c> and <c>PUT /recipes/{id}</c>: the full effective content.</summary>
/// <param name="Title">Title.</param>
/// <param name="Description">Description.</param>
/// <param name="YieldLoaves">Loaves the quantities are written for; a variant keeps the yield of its parent.</param>
/// <param name="LoafWeightG">Weight of one loaf, g.</param>
/// <param name="Notes">Notes.</param>
/// <param name="Ingredients">Ingredient lines.</param>
/// <param name="Stages">Stages in order.</param>
public sealed record RecipeRequest(
    string Title, string? Description, int YieldLoaves, decimal? LoafWeightG, string? Notes,
    IReadOnlyList<RecipeIngredientInput> Ingredients, IReadOnlyList<RecipeStageInput> Stages);

/// <summary>Body of <c>POST /recipes/{id}/variants</c>: a new variant inherits everything; change it with PUT.</summary>
/// <param name="Title">Title of the variant.</param>
public sealed record CreateVariantRequest(string Title);

/// <summary>Query of <c>GET /recipes</c>.</summary>
/// <param name="Search">Part of the title.</param>
/// <param name="IncludeArchived">Include archived recipes.</param>
/// <param name="MinProteinPercent">Only recipes with a flour of at least this protein (US-37).</param>
/// <param name="Page">Page, from 1.</param>
/// <param name="PageSize">Page size, 1-100.</param>
public sealed record RecipeListQuery(
    string? Search = null, bool IncludeArchived = false, decimal? MinProteinPercent = null, int Page = 1,
    int PageSize = Paging.DefaultPageSize);

/// <summary>Query with a number of loaves (scaling, cost, production figures).</summary>
/// <param name="Loaves">Number of loaves, 1-1000.</param>
public sealed record LoavesQuery(int Loaves = 1);

/// <summary>Scaled ingredient (US-06).</summary>
/// <param name="LineId">Line id.</param>
/// <param name="IngredientId">Ingredient.</param>
/// <param name="IngredientName">Name.</param>
/// <param name="UnitCode">Unit.</param>
/// <param name="Quantity">Quantity for the requested loaves.</param>
/// <param name="IsOptional">Optional ingredient.</param>
public sealed record ScaledIngredientDto(
    Guid LineId, Guid IngredientId, string IngredientName, string UnitCode, decimal Quantity, bool IsOptional);

/// <summary>Recipe scaled to a number of loaves (US-06).</summary>
/// <param name="RecipeId">Recipe.</param>
/// <param name="YieldLoaves">Loaves the recipe is written for.</param>
/// <param name="Loaves">Requested loaves.</param>
/// <param name="Ingredients">Scaled quantities.</param>
public sealed record ScaledRecipeDto(Guid RecipeId, int YieldLoaves, int Loaves, IReadOnlyList<ScaledIngredientDto> Ingredients);

/// <summary>How a line of a variant differs from the parent (US-08).</summary>
public enum ChangeKind
{
    /// <summary>Inherited without changes.</summary>
    Unchanged,

    /// <summary>Replaced by the variant.</summary>
    Changed,

    /// <summary>Added by the variant.</summary>
    Added,

    /// <summary>Removed by the variant.</summary>
    Removed,
}

/// <summary>Compared ingredient line.</summary>
/// <param name="Change">Kind of change.</param>
/// <param name="Parent">Line in the parent (null when added).</param>
/// <param name="Variant">Line in the variant (null when removed).</param>
public sealed record IngredientComparisonDto(ChangeKind Change, RecipeIngredientDto? Parent, RecipeIngredientDto? Variant);

/// <summary>Compared stage.</summary>
/// <param name="Change">Kind of change.</param>
/// <param name="Parent">Stage in the parent (null when added).</param>
/// <param name="Variant">Stage in the variant (null when removed).</param>
public sealed record StageComparisonDto(ChangeKind Change, RecipeStageDto? Parent, RecipeStageDto? Variant);

/// <summary>Variant compared with its parent (US-08).</summary>
/// <param name="RecipeId">Variant.</param>
/// <param name="ParentRecipeId">Parent.</param>
/// <param name="ChangedFields">Changed scalar fields (camelCase names).</param>
/// <param name="Ingredients">Ingredient lines with their change.</param>
/// <param name="Stages">Stages with their change.</param>
public sealed record RecipeComparisonDto(
    Guid RecipeId, Guid ParentRecipeId, IReadOnlyList<string> ChangedFields,
    IReadOnlyList<IngredientComparisonDto> Ingredients, IReadOnlyList<StageComparisonDto> Stages);

/// <summary>An own ingredient row of a recipe as stored (base lines, or replacements/removals/additions of a variant).</summary>
/// <param name="Id">Row id; null = to insert.</param>
/// <param name="IngredientId">Ingredient.</param>
/// <param name="Quantity">Quantity.</param>
/// <param name="IsOptional">Optional.</param>
/// <param name="SortOrder">Order.</param>
/// <param name="Note">Note.</param>
/// <param name="ReplacesItemId">Variant: parent line replaced or removed.</param>
/// <param name="IsRemoved">Variant: the parent line is removed.</param>
public sealed record OwnIngredientRow(
    Guid? Id, Guid IngredientId, decimal Quantity, bool IsOptional, int SortOrder, string? Note, Guid? ReplacesItemId,
    bool IsRemoved);

/// <summary>An own stage row of a recipe as stored.</summary>
/// <param name="Id">Row id; null = to insert.</param>
/// <param name="SortOrder">Order.</param>
/// <param name="Name">Name.</param>
/// <param name="StageTypeCode">Stage type.</param>
/// <param name="MinDurationMinutes">Minimum duration.</param>
/// <param name="MaxDurationMinutes">Maximum duration.</param>
/// <param name="ActiveMinutes">Hands-on time.</param>
/// <param name="ProofMode">Proofing mode.</param>
/// <param name="TemperatureC">Temperature.</param>
/// <param name="UsesOven">Uses the oven.</param>
/// <param name="UsesMixer">Uses the mixer.</param>
/// <param name="IsOptional">Optional.</param>
/// <param name="Instructions">Instructions.</param>
/// <param name="ReplacesStageId">Variant: parent stage replaced or removed.</param>
/// <param name="IsRemoved">Variant: the parent stage is removed.</param>
public sealed record OwnStageRow(
    Guid? Id, int SortOrder, string Name, string StageTypeCode, int MinDurationMinutes, int MaxDurationMinutes,
    int ActiveMinutes, ProofMode? ProofMode, decimal? TemperatureC, bool UsesOven, bool UsesMixer, bool IsOptional,
    string? Instructions, Guid? ReplacesStageId, bool IsRemoved);

/// <summary>What a recipe stores itself: header columns and own rows (used to compute changes).</summary>
/// <param name="Id">Id.</param>
/// <param name="ParentRecipeId">Parent of a variant.</param>
/// <param name="YieldLoaves">Yield.</param>
/// <param name="Ingredients">Own ingredient rows.</param>
/// <param name="Stages">Own stage rows.</param>
public sealed record RecipeState(
    Guid Id, Guid? ParentRecipeId, int YieldLoaves, IReadOnlyList<OwnIngredientRow> Ingredients,
    IReadOnlyList<OwnStageRow> Stages);

/// <summary>Header to store. For a variant, description, loaf weight and notes go to the overrides JSON.</summary>
/// <param name="Title">Title.</param>
/// <param name="YieldLoaves">Yield.</param>
/// <param name="Description">Description (base recipes).</param>
/// <param name="LoafWeightG">Loaf weight (base recipes).</param>
/// <param name="Notes">Notes (base recipes).</param>
/// <param name="VariantOverridesJson">Overrides JSON (variants), or null.</param>
public sealed record RecipeHeaderChange(
    string Title, int YieldLoaves, string? Description, decimal? LoafWeightG, string? Notes, string? VariantOverridesJson);

/// <summary>Complete set of own rows after a change: rows with an id are updated, without an id inserted, others deleted.</summary>
/// <param name="Header">Header columns.</param>
/// <param name="Ingredients">All own ingredient rows.</param>
/// <param name="Stages">All own stage rows.</param>
public sealed record RecipeChangeSet(
    RecipeHeaderChange Header, IReadOnlyList<OwnIngredientRow> Ingredients, IReadOnlyList<OwnStageRow> Stages);

/// <summary>Data access for recipes; every method is scoped to the owner.</summary>
public interface IRecipeRepository
{
    /// <summary>Page of recipes ordered by title.</summary>
    Task<PagedResult<RecipeSummaryDto>> ListAsync(Guid ownerId, RecipeListQuery query, CancellationToken cancellationToken);

    /// <summary>Effective recipe (through the effective content functions), or null.</summary>
    Task<RecipeDto?> GetEffectiveAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Own header and rows, or null.</summary>
    Task<RecipeState?> GetStateAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>True when the recipe exists for the owner.</summary>
    Task<bool> ExistsAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Creates a recipe (or a variant when <paramref name="parentRecipeId"/> is set) in one transaction.</summary>
    Task<Guid> CreateAsync(Guid ownerId, Guid? parentRecipeId, RecipeChangeSet recipe, CancellationToken cancellationToken);

    /// <summary>Stores the change set in one transaction; false when not found.</summary>
    Task<bool> SaveAsync(Guid ownerId, Guid id, RecipeChangeSet changes, CancellationToken cancellationToken);

    /// <summary>Sets the archived flag; false when not found.</summary>
    Task<bool> SetArchivedAsync(Guid ownerId, Guid id, bool archived, CancellationToken cancellationToken);

    /// <summary>True when the recipe has variants or is used in a batch.</summary>
    Task<bool> IsReferencedAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);

    /// <summary>Deletes the recipe; false when not found.</summary>
    Task<bool> DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken);
}
