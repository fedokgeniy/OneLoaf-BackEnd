using Fluxor;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>What the editor needs besides the form: the recipe (when editing), the ingredient catalog, the stage types.</summary>
public sealed record EditorData(
    RecipeDto? Recipe,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<StageTypeDto> StageTypes,
    IReadOnlyList<IngredientCategoryDto>? Categories = null,
    IReadOnlyList<UnitDto>? Units = null);

/// <summary>Recipe editor slice (US-05, US-07, US-10). The form itself is local state of the page.</summary>
[FeatureState(Name = "RecipeEditor")]
public sealed record RecipeEditorState(Guid? RecipeId, Loadable<EditorData> Data, bool Busy)
{
    private RecipeEditorState()
        : this(null, Loadable<EditorData>.Idle, false)
    {
    }
}

/// <summary>Intent: open the editor for a new recipe (null) or an existing one.</summary>
public sealed record OpenEditor(Guid? Id);

/// <summary>Result: editor data loaded.</summary>
public sealed record EditorLoaded(Guid? Id, EditorData Data);

/// <summary>Result: editor data failed.</summary>
public sealed record EditorLoadFailed(Guid? Id, ApiError Error);

/// <summary>Intent: create (<paramref name="Id"/> null) or update a recipe.</summary>
public sealed record SaveRecipe(Guid? Id, RecipeRequest Request);

/// <summary>Intent: add an ingredient to the catalog without leaving the editor.</summary>
public sealed record CreateEditorIngredient(IngredientRequest Request);

/// <summary>Result: the ingredient exists and can be used in a line.</summary>
public sealed record EditorIngredientCreated(IngredientDto Ingredient);

/// <summary>Result: the ingredient could not be created.</summary>
public sealed record EditorIngredientFailed(ApiError Error);

/// <summary>Intent: create a variant of <paramref name="ParentId"/> (US-07).</summary>
public sealed record CreateVariant(Guid ParentId, string Title);

/// <summary>Result: a recipe was created or updated (also a new variant).</summary>
public sealed record RecipeSaved(RecipeDto Recipe, bool Created);

/// <summary>Result: saving failed; the page maps field errors.</summary>
public sealed record RecipeSaveFailed(ApiError Error);

/// <summary>Intent: archive or restore a recipe.</summary>
public sealed record SetRecipeArchived(Guid Id, bool Archived);

/// <summary>Result: archived flag changed.</summary>
public sealed record RecipeArchivedChanged(Guid Id, bool Archived);

/// <summary>Intent: delete a recipe.</summary>
public sealed record DeleteRecipe(Guid Id);

/// <summary>Result: deleted.</summary>
public sealed record RecipeDeleted(Guid Id);

/// <summary>Result: archive or delete failed (409: has variants or batches).</summary>
public sealed record RecipeCommandFailed(ApiError Error);

/// <summary>Pure reducers of the editor slice, and the invalidation of the list and detail slices after a change.</summary>
public static class RecipeEditorReducers
{
    /// <summary>Loading starts; reopening the same recipe keeps the data while it reloads.</summary>
    [ReducerMethod]
    public static RecipeEditorState OnOpen(RecipeEditorState state, OpenEditor action) =>
        new(action.Id, state.RecipeId == action.Id ? state.Data.Reload() : new Loadable<EditorData>.Loading(null), false);

    /// <summary>Data for the current editor.</summary>
    [ReducerMethod]
    public static RecipeEditorState OnLoaded(RecipeEditorState state, EditorLoaded action) =>
        action.Id == state.RecipeId ? state with { Data = new Loadable<EditorData>.Loaded(action.Data) } : state;

    /// <summary>Data failed.</summary>
    [ReducerMethod]
    public static RecipeEditorState OnLoadFailed(RecipeEditorState state, EditorLoadFailed action) =>
        action.Id == state.RecipeId ? state with { Data = new Loadable<EditorData>.Failed(action.Error) } : state;

    /// <summary>The new ingredient joins the catalog the editor offers.</summary>
    [ReducerMethod]
    public static RecipeEditorState OnIngredientCreated(RecipeEditorState state, EditorIngredientCreated action) =>
        state.Data.Value is { } data
            ? state with { Data = new Loadable<EditorData>.Loaded(data with { Ingredients = [.. data.Ingredients, action.Ingredient] }) }
            : state;

    /// <summary>The ingredient list of the catalog page is stale.</summary>
    [ReducerMethod(typeof(EditorIngredientCreated))]
    public static CatalogState CatalogAfterIngredient(CatalogState state) =>
        state with { List = Loadable<OneLoaf.Web.Shared.Contracts.PagedResult<IngredientDto>>.Idle };

    /// <summary>A save, variant, archive or delete request runs.</summary>
    [ReducerMethod(typeof(SaveRecipe))]
    public static RecipeEditorState OnSave(RecipeEditorState state) => state with { Busy = true };

    /// <summary>Variant request runs.</summary>
    [ReducerMethod(typeof(CreateVariant))]
    public static RecipeEditorState OnCreateVariant(RecipeEditorState state) => state with { Busy = true };

    /// <summary>Archive request runs.</summary>
    [ReducerMethod(typeof(SetRecipeArchived))]
    public static RecipeEditorState OnArchive(RecipeEditorState state) => state with { Busy = true };

    /// <summary>Delete request runs.</summary>
    [ReducerMethod(typeof(DeleteRecipe))]
    public static RecipeEditorState OnDelete(RecipeEditorState state) => state with { Busy = true };

    /// <summary>Saved: the editor starts fresh next time.</summary>
    [ReducerMethod(typeof(RecipeSaved))]
    public static RecipeEditorState OnSaved(RecipeEditorState state) => state with { Busy = false, Data = Loadable<EditorData>.Idle, RecipeId = null };

    /// <summary>Request finished without leaving the page.</summary>
    [ReducerMethod(typeof(RecipeSaveFailed))]
    public static RecipeEditorState OnSaveFailed(RecipeEditorState state) => state with { Busy = false };

    /// <summary>Archive flag changed.</summary>
    [ReducerMethod(typeof(RecipeArchivedChanged))]
    public static RecipeEditorState OnArchived(RecipeEditorState state) => state with { Busy = false, Data = Loadable<EditorData>.Idle, RecipeId = null };

    /// <summary>Deleted.</summary>
    [ReducerMethod(typeof(RecipeDeleted))]
    public static RecipeEditorState OnDeleted(RecipeEditorState state) => state with { Busy = false, Data = Loadable<EditorData>.Idle, RecipeId = null };

    /// <summary>Command failed.</summary>
    [ReducerMethod(typeof(RecipeCommandFailed))]
    public static RecipeEditorState OnCommandFailed(RecipeEditorState state) => state with { Busy = false };

    /// <summary>The list reloads on the next visit after any change.</summary>
    [ReducerMethod(typeof(RecipeSaved))]
    public static RecipesState ListAfterSave(RecipesState state) => state with { List = Loadable<RecipeList>.Idle };

    /// <summary>The list reloads after archive or restore.</summary>
    [ReducerMethod(typeof(RecipeArchivedChanged))]
    public static RecipesState ListAfterArchive(RecipesState state) => state with { List = Loadable<RecipeList>.Idle };

    /// <summary>The list reloads after delete.</summary>
    [ReducerMethod(typeof(RecipeDeleted))]
    public static RecipesState ListAfterDelete(RecipesState state) => state with { List = Loadable<RecipeList>.Idle };

    /// <summary>The detail reloads after a save.</summary>
    [ReducerMethod(typeof(RecipeSaved))]
    public static RecipeDetailState DetailAfterSave(RecipeDetailState state) => RecipeDetailState.Initial;

    /// <summary>The detail reloads after archive or restore.</summary>
    [ReducerMethod(typeof(RecipeArchivedChanged))]
    public static RecipeDetailState DetailAfterArchive(RecipeDetailState state) => RecipeDetailState.Initial;

    /// <summary>The detail reloads after delete.</summary>
    [ReducerMethod(typeof(RecipeDeleted))]
    public static RecipeDetailState DetailAfterDelete(RecipeDetailState state) => RecipeDetailState.Initial;
}
