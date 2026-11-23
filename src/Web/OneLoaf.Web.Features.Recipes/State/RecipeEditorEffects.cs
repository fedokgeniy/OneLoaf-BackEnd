using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Reference;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>Side effects of the editor: load, save, variant, archive, delete — with navigation and toasts.</summary>
public sealed class RecipeEditorEffects(
    IRecipesApi recipes, ICatalogApi catalog, IReferenceApi reference, NavigationManager navigation)
{
    /// <summary>Loads recipe, ingredients and stage types in parallel.</summary>
    [EffectMethod]
    public async Task HandleOpen(OpenEditor action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var recipeTask = action.Id is { } id ? recipes.GetAsync(id) : null;
        var ingredientsTask = catalog.ListIngredientsAsync(null, null, 1, ICatalogApi.MaxPageSize);
        var stageTypesTask = reference.GetStageTypesAsync();
        var recipe = recipeTask is null ? null : await recipeTask;
        var ingredients = await ingredientsTask;
        var stageTypes = await stageTypesTask;

        var error = (recipe as ApiResult<RecipeDto>.Failure)?.Error
            ?? (ingredients as ApiResult<PagedResult<IngredientDto>>.Failure)?.Error
            ?? (stageTypes as ApiResult<IReadOnlyList<StageTypeDto>>.Failure)?.Error;
        if (error is not null
            || ingredients is not ApiResult<PagedResult<IngredientDto>>.Success { Value: var page }
            || stageTypes is not ApiResult<IReadOnlyList<StageTypeDto>>.Success { Value: var types })
        {
            dispatcher.Dispatch(new EditorLoadFailed(action.Id, error ?? ApiError.FromStatus(500)));
            return;
        }

        dispatcher.Dispatch(new EditorLoaded(action.Id,
            new EditorData((recipe as ApiResult<RecipeDto>.Success)?.Value, page.Items, types)));
    }

    /// <summary>Creates or updates, then opens the recipe.</summary>
    [EffectMethod]
    public async Task HandleSave(SaveRecipe action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = action.Id is { } id
            ? await recipes.UpdateAsync(id, action.Request)
            : await recipes.CreateAsync(action.Request);
        Complete(result, created: action.Id is null, dispatcher);
    }

    /// <summary>Creates a variant, then opens it.</summary>
    [EffectMethod]
    public async Task HandleCreateVariant(CreateVariant action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        Complete(await recipes.CreateVariantAsync(action.ParentId, new CreateVariantRequest(action.Title.Trim())), created: true, dispatcher);
    }

    /// <summary>Archives or restores.</summary>
    [EffectMethod]
    public async Task HandleSetArchived(SetRecipeArchived action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await recipes.SetArchivedAsync(action.Id, action.Archived))
        {
            case ApiResult.Failure { Error: var error }:
                Fail(error, dispatcher);
                break;
            default:
                dispatcher.Dispatch(new RecipeArchivedChanged(action.Id, action.Archived));
                dispatcher.Dispatch(new ShowToast(Toast.Success(action.Archived ? "Recipe archived" : "Recipe restored")));
                navigation.NavigateTo($"recipes/{action.Id}");
                break;
        }
    }

    /// <summary>Deletes; a recipe with variants or batches answers 409 and must be archived instead.</summary>
    [EffectMethod]
    public async Task HandleDelete(DeleteRecipe action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await recipes.DeleteAsync(action.Id))
        {
            case ApiResult.Failure { Error: var error }:
                Fail(error, dispatcher);
                break;
            default:
                dispatcher.Dispatch(new RecipeDeleted(action.Id));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Recipe deleted")));
                navigation.NavigateTo("recipes");
                break;
        }
    }

    private static void Fail(ApiError error, IDispatcher dispatcher)
    {
        dispatcher.Dispatch(new RecipeCommandFailed(error));
        dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
    }

    private void Complete(ApiResult<RecipeDto> result, bool created, IDispatcher dispatcher)
    {
        switch (result)
        {
            case ApiResult<RecipeDto>.Success { Value: var recipe }:
                dispatcher.Dispatch(new RecipeSaved(recipe, created));
                dispatcher.Dispatch(new ShowToast(Toast.Success(created ? "Recipe created" : "Recipe saved")));
                navigation.NavigateTo($"recipes/{recipe.Id}");
                break;
            case ApiResult<RecipeDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new RecipeSaveFailed(error));
                break;
        }
    }
}
