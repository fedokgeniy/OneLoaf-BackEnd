using System.Collections.Immutable;
using Fluxor;

namespace OneLoaf.Web.Shared.State.Photos;

/// <summary>
/// Photos shown on cards of lists (recipes, library, history), as data URLs by key. A photo is downloaded once and
/// kept for the session, so going back to a list shows it at once.
/// </summary>
[FeatureState(Name = "Photos")]
public sealed record PhotosState(ImmutableDictionary<string, string> Sources)
{
    private PhotosState()
        : this(ImmutableDictionary<string, string>.Empty)
    {
    }

    /// <summary>The data URL of a photo; null while it is not loaded or when there is none.</summary>
    public string? For(string key) => Sources.GetValueOrDefault(key);
}

/// <summary>Keys of the photos in <see cref="PhotosState"/>.</summary>
public static class PhotoKeys
{
    /// <summary>The photo of an own recipe.</summary>
    public static string Recipe(Guid id) => $"recipes/{id:N}";

    /// <summary>The photo of a published recipe.</summary>
    public static string Publication(Guid id) => $"library/{id:N}";

    /// <summary>A crumb photo of a bake record; another photo is another key.</summary>
    public static string Bake(Guid recordId, Guid photoId) => $"bakes/{recordId:N}/{photoId:N}";
}

/// <summary>Result: a photo arrived (or, with a null source, was removed).</summary>
public sealed record PhotoCached(string Key, string? Src);

/// <summary>Reducers of <see cref="PhotosState"/>.</summary>
public static class PhotosReducers
{
    /// <summary>Keeps the photo, or forgets it when it was removed.</summary>
    [ReducerMethod]
    public static PhotosState OnCached(PhotosState state, PhotoCached action)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);
        return new PhotosState(action.Src is null ? state.Sources.Remove(action.Key) : state.Sources.SetItem(action.Key, action.Src));
    }
}
