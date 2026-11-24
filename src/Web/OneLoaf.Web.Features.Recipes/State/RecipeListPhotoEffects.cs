using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State.Photos;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>Loads the photos of the recipes in the list, one after another, and keeps the card of a changed photo right.</summary>
public sealed class RecipeListPhotoEffects(IRecipesApi api, IState<PhotosState> photos)
{
    /// <summary>The first page arrived.</summary>
    [EffectMethod]
    public Task HandleLoaded(RecipesLoaded action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        return LoadAsync(action.List.Items, dispatcher);
    }

    /// <summary>A further page arrived.</summary>
    [EffectMethod]
    public Task HandleMore(MoreRecipesLoaded action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        return LoadAsync(action.Page.Items, dispatcher);
    }

    /// <summary>The photo of a recipe was loaded, saved or removed on its page: the list shows the same.</summary>
    [EffectMethod]
    public Task HandleChanged(RecipePhotoLoaded action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var key = PhotoKeys.Recipe(action.Id);
        if (photos.Value.For(key) != action.Src)
        {
            dispatcher.Dispatch(new PhotoCached(key, action.Src));
        }

        return Task.CompletedTask;
    }

    private async Task LoadAsync(IEnumerable<RecipeSummaryDto> recipes, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        foreach (var recipe in recipes.Where(r => r.HasPhoto))
        {
            var key = PhotoKeys.Recipe(recipe.Id);
            if (photos.Value.For(key) is null && await api.GetPhotoAsync(recipe.Id) is ApiResult<FileDownload>.Success { Value: var file })
            {
                dispatcher.Dispatch(new PhotoCached(key, file.ToDataUrl()));
            }
        }
    }
}
