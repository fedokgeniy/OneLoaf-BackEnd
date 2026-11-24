using Fluxor;
using OneLoaf.Web.Features.Library.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Library.State;

/// <summary>Publish dialog (US-39): the recipe to publish and the request.</summary>
[FeatureState(Name = "Publish")]
public sealed record PublishState(Guid? RecipeId, Loadable<RecipeDto> Recipe, bool Publishing, ApiError? Error)
{
    private PublishState()
        : this(null, Loadable<RecipeDto>.Idle, false, null)
    {
    }
}

/// <summary>Intent: open the dialog for a recipe.</summary>
public sealed record OpenPublish(Guid RecipeId);

/// <summary>Result: the recipe.</summary>
public sealed record PublishRecipeLoaded(Guid RecipeId, Loadable<RecipeDto> Recipe);

/// <summary>Intent: publish.</summary>
public sealed record PublishRecipe;

/// <summary>Result: published; the page opens the publication.</summary>
public sealed record RecipePublished(Guid PublicationId);

/// <summary>Result: publishing failed.</summary>
public sealed record PublishFailed(ApiError Error);

/// <summary>Reducers of <see cref="PublishState"/>.</summary>
public static class PublishReducers
{
    /// <summary>Opening.</summary>
    [ReducerMethod]
    public static PublishState OnOpen(PublishState state, OpenPublish action) =>
        new(action.RecipeId, new Loadable<RecipeDto>.Loading(null), false, null);

    /// <summary>Recipe.</summary>
    [ReducerMethod]
    public static PublishState OnLoaded(PublishState state, PublishRecipeLoaded action) =>
        action.RecipeId == state.RecipeId ? state with { Recipe = action.Recipe } : state;

    /// <summary>Publishing.</summary>
    [ReducerMethod(typeof(PublishRecipe))]
    public static PublishState OnPublish(PublishState state) => state with { Publishing = true, Error = null };

    /// <summary>Published.</summary>
    [ReducerMethod(typeof(RecipePublished))]
    public static PublishState OnPublished(PublishState state) => state with { Publishing = false };

    /// <summary>Failed.</summary>
    [ReducerMethod]
    public static PublishState OnFailed(PublishState state, PublishFailed action) => state with { Publishing = false, Error = action.Error };

    /// <summary>The library list shows the new publication after a reload.</summary>
    [ReducerMethod(typeof(RecipePublished))]
    public static LibraryState ListAfterPublish(LibraryState state) => state with { List = Loadable<PagedList<LibraryItemDto>>.Idle };
}

/// <summary>Side effects of the publish dialog.</summary>
public sealed class PublishEffects(ILibraryApi api, IState<PublishState> state)
{
    /// <summary>Loads the recipe title.</summary>
    [EffectMethod]
    public async Task HandleOpen(OpenPublish action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        dispatcher.Dispatch(new PublishRecipeLoaded(action.RecipeId, (await api.GetRecipeAsync(action.RecipeId)).ToLoadable()));
    }

    /// <summary>Publishes a snapshot of the recipe.</summary>
    [EffectMethod(typeof(PublishRecipe))]
    public async Task HandlePublish(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.RecipeId is not { } recipeId)
        {
            return;
        }

        switch (await api.PublishAsync(recipeId))
        {
            case ApiResult<PublishResponse>.Success { Value: var published }:
                dispatcher.Dispatch(new RecipePublished(published.PublishedRecipeId));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Published to the library")));
                break;
            case ApiResult<PublishResponse>.Failure { Error: var error }:
                dispatcher.Dispatch(new PublishFailed(error));
                break;
        }
    }
}
