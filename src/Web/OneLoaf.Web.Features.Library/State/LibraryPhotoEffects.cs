using Fluxor;
using OneLoaf.Web.Features.Library.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State.Photos;

namespace OneLoaf.Web.Features.Library.State;

/// <summary>Loads the photos of published recipes: for the cards of the library and for an opened publication.</summary>
public sealed class LibraryPhotoEffects(ILibraryApi api, IState<PhotosState> photos)
{
    /// <summary>The first page arrived.</summary>
    [EffectMethod]
    public Task HandleLoaded(LibraryLoaded action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        return LoadAsync(action.List.Value?.Items.Where(i => i.HasPhoto).Select(i => i.Id) ?? [], dispatcher);
    }

    /// <summary>A further page arrived.</summary>
    [EffectMethod]
    public Task HandleMore(MoreLibraryLoaded action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        return LoadAsync(action.Page?.Items.Where(i => i.HasPhoto).Select(i => i.Id) ?? [], dispatcher);
    }

    /// <summary>A publication was opened.</summary>
    [EffectMethod]
    public Task HandlePublication(PublicationLoaded action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        return LoadAsync(action.Publication.Value is { HasPhoto: true } publication ? [publication.Id] : [], dispatcher);
    }

    private async Task LoadAsync(IEnumerable<Guid> ids, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        foreach (var id in ids)
        {
            var key = PhotoKeys.Publication(id);
            if (photos.Value.For(key) is null && await api.GetPhotoAsync(id) is ApiResult<FileDownload>.Success { Value: var file })
            {
                dispatcher.Dispatch(new PhotoCached(key, file.ToDataUrl()));
            }
        }
    }
}
