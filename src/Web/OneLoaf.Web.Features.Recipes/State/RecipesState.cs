using System.Collections.Immutable;
using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>Loaded part of the recipe list: all pages fetched so far.</summary>
public sealed record RecipeList(ImmutableList<RecipeSummaryDto> Items, int Page, int TotalCount, bool HasMore);

/// <summary>Recipes slice: the list, its filter (kept when the user comes back from a recipe) and paging.</summary>
[FeatureState(Name = "Recipes")]
public sealed record RecipesState(RecipeQuery Query, bool VariantsOnly, Loadable<RecipeList> List, bool LoadingMore)
{
    private RecipesState()
        : this(RecipeQuery.None, false, Loadable<RecipeList>.Idle, false)
    {
    }
}

/// <summary>Intent: change the server-side filter; <paramref name="Debounce"/> while the user is typing.</summary>
public sealed record SetRecipeQuery(RecipeQuery Query, bool Debounce);

/// <summary>Intent: show only variants of the loaded recipes (client-side, no request).</summary>
public sealed record SetVariantsOnly(bool VariantsOnly);

/// <summary>Intent: load the first page for the current filter.</summary>
public sealed record LoadRecipes(RecipeQuery Query);

/// <summary>Result: first page loaded.</summary>
public sealed record RecipesLoaded(RecipeQuery Query, RecipeList List);

/// <summary>Result: the first page failed.</summary>
public sealed record RecipesLoadFailed(RecipeQuery Query, ApiError Error);

/// <summary>Intent: load the next page.</summary>
public sealed record LoadMoreRecipes;

/// <summary>Result: the next page arrived and is appended.</summary>
public sealed record MoreRecipesLoaded(RecipeQuery Query, RecipeList Page);

/// <summary>Result: the next page failed (shown as a toast; the loaded pages stay).</summary>
public sealed record MoreRecipesLoadFailed(ApiError Error);

/// <summary>Pure reducers of <see cref="RecipesState"/>.</summary>
public static class RecipesReducers
{
    /// <summary>New filter; the old cards stay visible until the new page arrives.</summary>
    [ReducerMethod]
    public static RecipesState OnSetQuery(RecipesState state, SetRecipeQuery action) =>
        state with { Query = action.Query, List = state.List.Reload(), LoadingMore = false };

    /// <summary>Variants toggle.</summary>
    [ReducerMethod]
    public static RecipesState OnSetVariantsOnly(RecipesState state, SetVariantsOnly action) =>
        state with { VariantsOnly = action.VariantsOnly };

    /// <summary>Request starts.</summary>
    [ReducerMethod]
    public static RecipesState OnLoad(RecipesState state, LoadRecipes action) =>
        state with { Query = action.Query, List = state.List.Reload(), LoadingMore = false };

    /// <summary>A response for an older filter is dropped (the user kept typing).</summary>
    [ReducerMethod]
    public static RecipesState OnLoaded(RecipesState state, RecipesLoaded action) =>
        action.Query == state.Query ? state with { List = new Loadable<RecipeList>.Loaded(action.List) } : state;

    /// <summary>First page failed.</summary>
    [ReducerMethod]
    public static RecipesState OnLoadFailed(RecipesState state, RecipesLoadFailed action) =>
        action.Query == state.Query ? state with { List = new Loadable<RecipeList>.Failed(action.Error) } : state;

    /// <summary>Next page requested (only from a loaded list with more items).</summary>
    [ReducerMethod(typeof(LoadMoreRecipes))]
    public static RecipesState OnLoadMore(RecipesState state) =>
        state.List is Loadable<RecipeList>.Loaded { Data.HasMore: true } ? state with { LoadingMore = true } : state;

    /// <summary>Appends the next page.</summary>
    [ReducerMethod]
    public static RecipesState OnMoreLoaded(RecipesState state, MoreRecipesLoaded action) =>
        action.Query == state.Query && state.List is Loadable<RecipeList>.Loaded { Data: var list }
            ? state with
            {
                List = new Loadable<RecipeList>.Loaded(action.Page with { Items = list.Items.AddRange(action.Page.Items) }),
                LoadingMore = false,
            }
            : state with { LoadingMore = false };

    /// <summary>Next page failed.</summary>
    [ReducerMethod(typeof(MoreRecipesLoadFailed))]
    public static RecipesState OnMoreFailed(RecipesState state) => state with { LoadingMore = false };
}
