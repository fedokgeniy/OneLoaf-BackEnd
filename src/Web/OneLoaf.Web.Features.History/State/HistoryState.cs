using System.Collections.Immutable;
using Fluxor;
using OneLoaf.Web.Features.History.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.History.State;

/// <summary>Loaded bake records.</summary>
public sealed record HistoryList(ImmutableList<BakeRecordSummaryDto> Items, int Page, int TotalCount, bool HasMore);

/// <summary>History list slice (US-31, US-33): filter, records, statistics.</summary>
[FeatureState(Name = "History")]
public sealed record HistoryState(HistoryQuery Query, Loadable<HistoryList> List, Loadable<IReadOnlyList<BakeStatisticsDto>> Statistics, bool LoadingMore)
{
    private HistoryState()
        : this(HistoryQuery.None, Loadable<HistoryList>.Idle, Loadable<IReadOnlyList<BakeStatisticsDto>>.Idle, false)
    {
    }
}

/// <summary>Intent: load the list for a filter (and the statistics the first time).</summary>
public sealed record LoadHistory(HistoryQuery Query);

/// <summary>Result: first page.</summary>
public sealed record HistoryLoaded(HistoryQuery Query, Loadable<HistoryList> List);

/// <summary>Result: statistics.</summary>
public sealed record StatisticsLoaded(Loadable<IReadOnlyList<BakeStatisticsDto>> Statistics);

/// <summary>Intent: next page.</summary>
public sealed record LoadMoreHistory;

/// <summary>Result: next page appended.</summary>
public sealed record MoreHistoryLoaded(HistoryQuery Query, HistoryList Page);

/// <summary>Result: next page failed.</summary>
public sealed record MoreHistoryFailed;

/// <summary>Reducers of <see cref="HistoryState"/>.</summary>
public static class HistoryReducers
{
    /// <summary>Loading keeps the cards while the filter changes.</summary>
    [ReducerMethod]
    public static HistoryState OnLoad(HistoryState state, LoadHistory action) =>
        state with { Query = action.Query, List = state.List.Reload(), LoadingMore = false };

    /// <summary>Result for the current filter.</summary>
    [ReducerMethod]
    public static HistoryState OnLoaded(HistoryState state, HistoryLoaded action) =>
        action.Query == state.Query ? state with { List = action.List } : state;

    /// <summary>Statistics.</summary>
    [ReducerMethod]
    public static HistoryState OnStatistics(HistoryState state, StatisticsLoaded action) => state with { Statistics = action.Statistics };

    /// <summary>Next page requested.</summary>
    [ReducerMethod(typeof(LoadMoreHistory))]
    public static HistoryState OnLoadMore(HistoryState state) =>
        state.List is Loadable<HistoryList>.Loaded { Data.HasMore: true } ? state with { LoadingMore = true } : state;

    /// <summary>Next page appended.</summary>
    [ReducerMethod]
    public static HistoryState OnMore(HistoryState state, MoreHistoryLoaded action) =>
        action.Query == state.Query && state.List.Value is { } list
            ? state with { List = new Loadable<HistoryList>.Loaded(action.Page with { Items = list.Items.AddRange(action.Page.Items) }), LoadingMore = false }
            : state with { LoadingMore = false };

    /// <summary>Next page failed.</summary>
    [ReducerMethod(typeof(MoreHistoryFailed))]
    public static HistoryState OnMoreFailed(HistoryState state) => state with { LoadingMore = false };

    /// <summary>A completed batch adds a record (US-31): the history reloads on the next visit.</summary>
    [ReducerMethod(typeof(BatchCompleted))]
    public static HistoryState OnBatchCompleted(HistoryState state) =>
        state with { List = Loadable<HistoryList>.Idle, Statistics = Loadable<IReadOnlyList<BakeStatisticsDto>>.Idle };
}

/// <summary>Side effects of the history list.</summary>
public sealed class HistoryEffects(IHistoryApi api, IState<HistoryState> state)
{
    /// <summary>First page, plus statistics when not loaded yet.</summary>
    [EffectMethod]
    public async Task HandleLoad(LoadHistory action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var statistics = state.Value.Statistics is Loadable<IReadOnlyList<BakeStatisticsDto>>.Loaded ? null : api.GetStatisticsAsync();
        var result = await api.ListAsync(action.Query, 1);
        dispatcher.Dispatch(new HistoryLoaded(action.Query, result switch
        {
            ApiResult<PagedResult<BakeRecordSummaryDto>>.Success { Value: var page } => new Loadable<HistoryList>.Loaded(ToList(page)),
            ApiResult<PagedResult<BakeRecordSummaryDto>>.Failure { Error: var error } => new Loadable<HistoryList>.Failed(error),
            _ => throw new InvalidOperationException(),
        }));
        if (statistics is not null)
        {
            dispatcher.Dispatch(new StatisticsLoaded((await statistics).ToLoadable()));
        }
    }

    /// <summary>Next page.</summary>
    [EffectMethod(typeof(LoadMoreHistory))]
    public async Task HandleLoadMore(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (!state.Value.LoadingMore || state.Value.List.Value is not { } list)
        {
            return;
        }

        var query = state.Value.Query;
        switch (await api.ListAsync(query, list.Page + 1))
        {
            case ApiResult<PagedResult<BakeRecordSummaryDto>>.Success { Value: var page }:
                dispatcher.Dispatch(new MoreHistoryLoaded(query, ToList(page)));
                break;
            case ApiResult<PagedResult<BakeRecordSummaryDto>>.Failure { Error: var error }:
                dispatcher.Dispatch(new MoreHistoryFailed());
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
        }
    }

    private static HistoryList ToList(PagedResult<BakeRecordSummaryDto> page) => new([.. page.Items], page.Page, page.TotalCount, page.HasMore);
}
