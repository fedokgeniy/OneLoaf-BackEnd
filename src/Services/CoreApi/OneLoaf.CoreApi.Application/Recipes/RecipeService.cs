using OneLoaf.CoreApi.Application.Catalog;
using OneLoaf.CoreApi.Application.Common;
using OneLoaf.CoreApi.Application.Reference;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Recipes;

/// <summary>Recipes and variants of the current user (US-05 - US-09).</summary>
public sealed class RecipeService(
    IRecipeRepository recipes,
    IIngredientRepository ingredients,
    ReferenceGuard reference,
    IWorkspaceTransaction transaction,
    ICurrentUser currentUser)
{
    /// <summary>Message for a missing recipe.</summary>
    public const string NotFoundMessage = "The recipe was not found.";

    /// <summary>Message when a referenced recipe is deleted.</summary>
    public const string InUseMessage = "The recipe has variants or is used in batches; archive it instead.";

    /// <summary>Message when a base recipe is compared.</summary>
    public const string NotVariantMessage = "Only a variant can be compared with its parent recipe.";

    /// <summary>Message for a proofing mode on a stage that is not a fermentation stage.</summary>
    public const string ProofModeMessage = "A proofing mode is allowed only for fermentation stages.";

    /// <summary>Page of recipes.</summary>
    public Task<PagedResult<RecipeSummaryDto>> ListAsync(RecipeListQuery query, CancellationToken cancellationToken) =>
        recipes.ListAsync(currentUser.Id, query, cancellationToken);

    /// <summary>Effective recipe.</summary>
    public async Task<RecipeDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await recipes.GetEffectiveAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);

    /// <summary>Creates a base recipe with its ingredients and stages in one transaction (US-05).</summary>
    public async Task<RecipeDto> CreateAsync(RecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var stageTypes = await ValidateContentAsync(request, cancellationToken);
        var id = await recipes.CreateAsync(currentUser.Id, null, RecipePlanner.PlanBase(request, null, stageTypes), cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>
    /// Creates a recipe whose ingredients are given by name: an ingredient of the user with the same name is reused,
    /// a missing one is created without a price. Everything is stored in one transaction, so a rejected recipe leaves
    /// no new ingredients behind.
    /// </summary>
    public async Task<RecipeDraftResponse> CreateDraftAsync(RecipeDraftRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var names = request.Ingredients.Select(i => i.Name.Trim()).ToList();
        var existing = await ingredients.FindByNamesAsync(currentUser.Id, names, cancellationToken);
        for (var i = 0; i < request.Ingredients.Count; i++)
        {
            if (!existing.ContainsKey(names[i]))
            {
                await reference.EnsureIngredientCategoryAsync(request.Ingredients[i].CategoryCode, $"ingredients[{i}].categoryCode", cancellationToken);
                await reference.EnsureUnitAsync(request.Ingredients[i].UnitCode, $"ingredients[{i}].unitCode", cancellationToken);
            }
        }

        return await transaction.ExecuteAsync(async ct =>
        {
            var created = new List<string>();
            var lines = new List<RecipeIngredientInput>(request.Ingredients.Count);
            for (var i = 0; i < request.Ingredients.Count; i++)
            {
                var draft = request.Ingredients[i];
                if (!existing.TryGetValue(names[i], out var ingredientId))
                {
                    ingredientId = await ingredients.CreateAsync(currentUser.Id,
                        new IngredientRequest(names[i], draft.CategoryCode, draft.UnitCode, null), ct);
                    created.Add(names[i]);
                }

                lines.Add(new RecipeIngredientInput(null, ingredientId, draft.Quantity, draft.IsOptional, draft.Note));
            }

            var recipe = await CreateAsync(
                new RecipeRequest(request.Title, request.Description, request.YieldLoaves, request.LoafWeightG, request.Notes,
                    lines, request.Stages), ct);
            return new RecipeDraftResponse(recipe, created);
        }, cancellationToken);
    }

    /// <summary>
    /// Replaces the content of a recipe. For a variant the request is the full effective content; only the
    /// differences from the parent are stored (US-07).
    /// </summary>
    public async Task<RecipeDto> UpdateAsync(Guid id, RecipeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var state = await recipes.GetStateAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);
        var stageTypes = await ValidateContentAsync(request, cancellationToken);

        RecipeChangeSet changes;
        if (state.ParentRecipeId is { } parentId)
        {
            var parent = await recipes.GetEffectiveAsync(currentUser.Id, parentId, cancellationToken)
                ?? throw new NotFoundException(NotFoundMessage);
            changes = RecipePlanner.PlanVariant(request, state, parent, stageTypes);
        }
        else
        {
            changes = RecipePlanner.PlanBase(request, state, stageTypes);
        }

        if (!await recipes.SaveAsync(currentUser.Id, id, changes, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Creates a variant that inherits everything from the recipe (US-07).</summary>
    public async Task<RecipeDto> CreateVariantAsync(Guid parentId, CreateVariantRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parent = await GetAsync(parentId, cancellationToken);
        var id = await recipes.CreateAsync(currentUser.Id, parentId, RecipePlanner.PlanNewVariant(request.Title, parent),
            cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Archives or restores a recipe.</summary>
    public async Task SetArchivedAsync(Guid id, bool archived, CancellationToken cancellationToken)
    {
        if (!await recipes.SetArchivedAsync(currentUser.Id, id, archived, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    /// <summary>Deletes a recipe that has no variants and is not used in batches.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await recipes.ExistsAsync(currentUser.Id, id, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        if (await recipes.IsReferencedAsync(currentUser.Id, id, cancellationToken))
        {
            throw new ConflictException(InUseMessage);
        }

        if (!await recipes.DeleteAsync(currentUser.Id, id, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }
    }

    /// <summary>Ingredient quantities for a number of loaves (US-06).</summary>
    public async Task<ScaledRecipeDto> GetScaledAsync(Guid id, int loaves, CancellationToken cancellationToken) =>
        RecipeViews.Scale(await GetAsync(id, cancellationToken), loaves);

    /// <summary>A variant compared with its parent (US-08).</summary>
    public async Task<RecipeComparisonDto> GetComparisonAsync(Guid id, CancellationToken cancellationToken)
    {
        var state = await recipes.GetStateAsync(currentUser.Id, id, cancellationToken) ?? throw new NotFoundException(NotFoundMessage);
        if (state.ParentRecipeId is not { } parentId)
        {
            throw new BusinessRuleException(NotVariantMessage);
        }

        var variant = await GetAsync(id, cancellationToken);
        var parent = await GetAsync(parentId, cancellationToken);
        return RecipeViews.Compare(variant, state, parent);
    }

    /// <summary>Checks stage codes, proofing modes and that every ingredient belongs to the user.</summary>
    private async Task<IReadOnlyDictionary<string, StageTypeDto>> ValidateContentAsync(
        RecipeRequest request, CancellationToken cancellationToken)
    {
        var stageTypes = await reference.GetStageTypesAsync(cancellationToken);
        for (var i = 0; i < request.Stages.Count; i++)
        {
            var stage = request.Stages[i];
            if (!stageTypes.TryGetValue(stage.StageTypeCode, out var type))
            {
                throw ValidationFailures.For($"stages[{i}].stageTypeCode", $"Unknown stage type '{stage.StageTypeCode}'.");
            }

            if (stage.ProofMode is not null && !type.IsFermentation)
            {
                throw ValidationFailures.For($"stages[{i}].proofMode", ProofModeMessage);
            }
        }

        var requested = request.Ingredients.Select(l => l.IngredientId).Distinct().ToList();
        var owned = await ingredients.GetOwnedIdsAsync(currentUser.Id, requested, cancellationToken);
        for (var i = 0; i < request.Ingredients.Count; i++)
        {
            if (!owned.Contains(request.Ingredients[i].IngredientId))
            {
                throw ValidationFailures.For($"ingredients[{i}].ingredientId", IngredientService.NotFoundMessage);
            }
        }

        return stageTypes;
    }
}
