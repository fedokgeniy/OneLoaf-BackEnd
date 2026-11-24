using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.State.Notifications;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>Photo of one recipe, shown on the recipe page and changed in the editor.</summary>
[FeatureState(Name = "RecipePhoto")]
public sealed record RecipePhotoState(Guid? RecipeId, string? Src, bool Loading, bool Busy)
{
    private RecipePhotoState()
        : this(null, null, false, false)
    {
    }
}

/// <summary>Intent: show the photo of a recipe; nothing is requested when the recipe has none.</summary>
public sealed record LoadRecipePhoto(Guid Id, bool HasPhoto);

/// <summary>Result: the photo as a data URL (null = none).</summary>
public sealed record RecipePhotoLoaded(Guid Id, string? Src);

/// <summary>Intent: upload the photo of a recipe.</summary>
public sealed record UploadRecipePhoto(Guid Id, PhotoFile Photo);

/// <summary>Intent: remove the photo of a recipe.</summary>
public sealed record RemoveRecipePhoto(Guid Id);

/// <summary>Result: a photo command failed.</summary>
public sealed record RecipePhotoFailed(Guid Id);

/// <summary>Reducers of <see cref="RecipePhotoState"/>.</summary>
public static class RecipePhotoReducers
{
    /// <summary>
    /// Another recipe: its photo replaces the previous one. For the same recipe a photo that is shown or loading stays,
    /// so a photo uploaded a moment ago is not lost to an older copy of the recipe.
    /// </summary>
    [ReducerMethod]
    public static RecipePhotoState OnLoad(RecipePhotoState state, LoadRecipePhoto action) =>
        action.Id == state.RecipeId && (state.Src is not null || state.Loading || !action.HasPhoto)
            ? state
            : new RecipePhotoState(action.Id, null, action.HasPhoto, false);

    /// <summary>The photo arrived, or was saved or removed.</summary>
    [ReducerMethod]
    public static RecipePhotoState OnLoaded(RecipePhotoState state, RecipePhotoLoaded action) =>
        action.Id == state.RecipeId ? state with { Src = action.Src, Loading = false, Busy = false } : state;

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(UploadRecipePhoto))]
    public static RecipePhotoState OnUpload(RecipePhotoState state) => state with { Busy = true };

    /// <summary>A command runs.</summary>
    [ReducerMethod(typeof(RemoveRecipePhoto))]
    public static RecipePhotoState OnRemove(RecipePhotoState state) => state with { Busy = true };

    /// <summary>A command failed.</summary>
    [ReducerMethod(typeof(RecipePhotoFailed))]
    public static RecipePhotoState OnFailed(RecipePhotoState state) => state with { Busy = false, Loading = false };
}

/// <summary>Side effects of the recipe photo.</summary>
public sealed class RecipePhotoEffects(IRecipesApi api, IState<RecipePhotoState> state)
{
    /// <summary>Downloads the photo with the bearer token.</summary>
    [EffectMethod]
    public async Task HandleLoad(LoadRecipePhoto action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (!state.Value.Loading || state.Value.RecipeId != action.Id || state.Value.Src is not null)
        {
            return;
        }

        dispatcher.Dispatch(await api.GetPhotoAsync(action.Id) is ApiResult<FileDownload>.Success { Value: var file }
            ? new RecipePhotoLoaded(action.Id, file.ToDataUrl())
            : new RecipePhotoFailed(action.Id));
    }

    /// <summary>Uploads the photo and shows it at once: the page already has the bytes.</summary>
    [EffectMethod]
    public async Task HandleUpload(UploadRecipePhoto action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.SetPhotoAsync(action.Id, action.Photo))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new RecipePhotoFailed(action.Id));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new RecipePhotoLoaded(action.Id,
                    new FileDownload(action.Photo.Content, action.Photo.ContentType, action.Photo.FileName).ToDataUrl()));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Photo saved")));
                break;
        }
    }

    /// <summary>Removes the photo.</summary>
    [EffectMethod]
    public async Task HandleRemove(RemoveRecipePhoto action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.DeletePhotoAsync(action.Id))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new RecipePhotoFailed(action.Id));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new RecipePhotoLoaded(action.Id, null));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Photo removed")));
                break;
        }
    }
}
