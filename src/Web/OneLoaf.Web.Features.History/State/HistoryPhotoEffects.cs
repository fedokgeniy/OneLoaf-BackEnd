using Fluxor;
using OneLoaf.Web.Features.History.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State.Photos;

namespace OneLoaf.Web.Features.History.State;

/// <summary>Loads the first crumb photo of every bake in the list for its card.</summary>
public sealed class HistoryPhotoEffects(IHistoryApi api, IState<PhotosState> photos)
{
    /// <summary>The first page arrived.</summary>
    [EffectMethod]
    public Task HandleLoaded(HistoryLoaded action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        return LoadAsync(action.List.Value?.Items ?? [], dispatcher);
    }

    /// <summary>A further page arrived.</summary>
    [EffectMethod]
    public Task HandleMore(MoreHistoryLoaded action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        return LoadAsync(action.Page.Items, dispatcher);
    }

    private async Task LoadAsync(IEnumerable<BakeRecordSummaryDto> bakes, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        foreach (var bake in bakes)
        {
            if (bake.CoverPhotoId is not { } photoId)
            {
                continue;
            }

            var key = PhotoKeys.Bake(bake.Id, photoId);
            if (photos.Value.For(key) is null
                && await api.GetPhotoAsync($"api/v1/bake-records/{bake.Id}/photos/{photoId}") is ApiResult<FileDownload>.Success { Value: var file })
            {
                dispatcher.Dispatch(new PhotoCached(key, file.ToDataUrl()));
            }
        }
    }
}
