using System.Collections.Immutable;
using Fluxor;
using OneLoaf.Web.Features.Library.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Library.State;

/// <summary>Loaded page set of a list.</summary>
public sealed record PagedList<T>(ImmutableList<T> Items, int Page, int TotalCount, bool HasMore)
{
    /// <summary>The next page appended.</summary>
    public PagedList<T> Append(PagedResult<T> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new(Items.AddRange(page.Items), page.Page, page.TotalCount, page.HasMore);
    }
}

/// <summary>Builds <see cref="PagedList{T}"/> values.</summary>
public static class PagedList
{
    /// <summary>First page of a result.</summary>
    public static PagedList<T> From<T>(PagedResult<T> page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new([.. page.Items], page.Page, page.TotalCount, page.HasMore);
    }
}

/// <summary>Public library list (US-40).</summary>
[FeatureState(Name = "Library")]
public sealed record LibraryState(string? Search, Loadable<PagedList<LibraryItemDto>> List, bool LoadingMore)
{
    private LibraryState()
        : this(null, Loadable<PagedList<LibraryItemDto>>.Idle, false)
    {
    }
}

/// <summary>Intent: search the library.</summary>
public sealed record SearchLibrary(string? Search);

/// <summary>Result: first page.</summary>
public sealed record LibraryLoaded(string? Search, Loadable<PagedList<LibraryItemDto>> List);

/// <summary>Intent: next page.</summary>
public sealed record LoadMoreLibrary;

/// <summary>Result: next page (or null when it failed).</summary>
public sealed record MoreLibraryLoaded(string? Search, PagedResult<LibraryItemDto>? Page);

/// <summary>Reducers of <see cref="LibraryState"/>.</summary>
public static class LibraryReducers
{
    /// <summary>Searching keeps the cards.</summary>
    [ReducerMethod]
    public static LibraryState OnSearch(LibraryState state, SearchLibrary action) =>
        state with { Search = action.Search, List = state.List.Reload(), LoadingMore = false };

    /// <summary>Result of the current search.</summary>
    [ReducerMethod]
    public static LibraryState OnLoaded(LibraryState state, LibraryLoaded action) =>
        action.Search == state.Search ? state with { List = action.List } : state;

    /// <summary>Next page requested.</summary>
    [ReducerMethod(typeof(LoadMoreLibrary))]
    public static LibraryState OnLoadMore(LibraryState state) =>
        state.List.Value is { HasMore: true } ? state with { LoadingMore = true } : state;

    /// <summary>Next page appended.</summary>
    [ReducerMethod]
    public static LibraryState OnMore(LibraryState state, MoreLibraryLoaded action) =>
        action.Search == state.Search && action.Page is { } page && state.List.Value is { } list
            ? state with { List = new Loadable<PagedList<LibraryItemDto>>.Loaded(list.Append(page)), LoadingMore = false }
            : state with { LoadingMore = false };
}

/// <summary>Side effects of the library list.</summary>
public sealed class LibraryEffects(ILibraryApi api, IState<LibraryState> state)
{
    /// <summary>First page.</summary>
    [EffectMethod]
    public async Task HandleSearch(SearchLibrary action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = await api.SearchAsync(action.Search, 1);
        dispatcher.Dispatch(new LibraryLoaded(action.Search, result switch
        {
            ApiResult<PagedResult<LibraryItemDto>>.Success { Value: var page } => new Loadable<PagedList<LibraryItemDto>>.Loaded(PagedList.From(page)),
            ApiResult<PagedResult<LibraryItemDto>>.Failure { Error: var error } => new Loadable<PagedList<LibraryItemDto>>.Failed(error),
            _ => throw new InvalidOperationException(),
        }));
    }

    /// <summary>Next page.</summary>
    [EffectMethod(typeof(LoadMoreLibrary))]
    public async Task HandleMore(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (!state.Value.LoadingMore || state.Value.List.Value is not { } list)
        {
            return;
        }

        var search = state.Value.Search;
        switch (await api.SearchAsync(search, list.Page + 1))
        {
            case ApiResult<PagedResult<LibraryItemDto>>.Success { Value: var page }:
                dispatcher.Dispatch(new MoreLibraryLoaded(search, page));
                break;
            case ApiResult<PagedResult<LibraryItemDto>>.Failure { Error: var error }:
                dispatcher.Dispatch(new MoreLibraryLoaded(search, null));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
        }
    }
}
