using Fluxor;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Contracts.Notification;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Home;

/// <summary>What Home shows (Home-* boards, US-23, US-28, US-31).</summary>
/// <param name="Active">The batch in progress with its stages, if any.</param>
/// <param name="Next">Otherwise the next planned batch.</param>
/// <param name="Starters">Active starters.</param>
/// <param name="RecentBakes">The three latest bake records.</param>
/// <param name="Reminders">Scheduled reminders of the next 24 hours; null when the Notification service failed.</param>
public sealed record HomeData(
    BatchDto? Active,
    BatchSummaryDto? Next,
    IReadOnlyList<StarterDto> Starters,
    IReadOnlyList<BakeRecordSummaryDto> RecentBakes,
    IReadOnlyList<ReminderDto>? Reminders);

/// <summary>Home slice.</summary>
[FeatureState(Name = "Home")]
public sealed record HomeState(Loadable<HomeData> Data)
{
    private HomeState()
        : this(Loadable<HomeData>.Idle)
    {
    }
}

/// <summary>Intent: load Home.</summary>
public sealed record LoadHome;

/// <summary>Result: Home loaded.</summary>
public sealed record HomeLoaded(Loadable<HomeData> Data);

/// <summary>Reducers of <see cref="HomeState"/>.</summary>
public static class HomeReducers
{
    /// <summary>Loading keeps the last data visible.</summary>
    [ReducerMethod(typeof(LoadHome))]
    public static HomeState OnLoad(HomeState state) => new(state.Data.Reload());

    /// <summary>Loaded.</summary>
    [ReducerMethod]
    public static HomeState OnLoaded(HomeState state, HomeLoaded action) => new(action.Data);

    /// <summary>A new batch changes Home (architecture.md §4: cross-feature chain).</summary>
    [ReducerMethod(typeof(BatchCreated))]
    public static HomeState OnBatchCreated(HomeState state) => new(Loadable<HomeData>.Idle);

    /// <summary>A completed batch changes Home.</summary>
    [ReducerMethod(typeof(BatchCompleted))]
    public static HomeState OnBatchCompleted(HomeState state) => new(Loadable<HomeData>.Idle);
}

/// <summary>Loads Home in parallel from the Core API and the Notification service.</summary>
public sealed class HomeEffects(ApiClientFactory api, TimeProvider time)
{
    /// <summary>Batches, starters, bakes and reminders at once; the active batch is then loaded with its stages.</summary>
    [EffectMethod(typeof(LoadHome))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        var now = time.GetUtcNow();
        var running = api.Core.GetAsync<PagedResult<BatchSummaryDto>>(Url.Build("api/v1/batches", ("status", BatchStatuses.InProgress), ("pageSize", 1)));
        var planned = api.Core.GetAsync<PagedResult<BatchSummaryDto>>(Url.Build("api/v1/batches", ("status", BatchStatuses.Planned), ("from", now), ("pageSize", 1)));
        var starters = api.Core.GetAsync<PagedResult<StarterDto>>(Url.Build("api/v1/starters", ("pageSize", 20)));
        var bakes = api.Core.GetAsync<PagedResult<BakeRecordSummaryDto>>(Url.Build("api/v1/bake-records", ("pageSize", 3)));
        var reminders = api.Notification.GetAsync<IReadOnlyList<ReminderDto>>(
            Url.Build("api/v1/reminders", ("status", "scheduled"), ("from", now), ("to", now.AddHours(24)), ("limit", 5)));

        var runningResult = await running;
        var plannedResult = await planned;
        var startersResult = await starters;
        var bakesResult = await bakes;
        var remindersResult = await reminders;

        var error = (runningResult as ApiResult<PagedResult<BatchSummaryDto>>.Failure)?.Error
            ?? (startersResult as ApiResult<PagedResult<StarterDto>>.Failure)?.Error
            ?? (bakesResult as ApiResult<PagedResult<BakeRecordSummaryDto>>.Failure)?.Error;
        if (error is not null)
        {
            dispatcher.Dispatch(new HomeLoaded(new Loadable<HomeData>.Failed(error)));
            return;
        }

        BatchDto? active = null;
        if (runningResult is ApiResult<PagedResult<BatchSummaryDto>>.Success { Value.Items: [var current, ..] }
            && await api.Core.GetAsync<BatchDto>($"api/v1/batches/{current.Id}") is ApiResult<BatchDto>.Success { Value: var batch })
        {
            active = batch;
        }

        dispatcher.Dispatch(new HomeLoaded(new Loadable<HomeData>.Loaded(new HomeData(
            active,
            plannedResult is ApiResult<PagedResult<BatchSummaryDto>>.Success { Value.Items: [var next, ..] } ? next : null,
            [.. ((ApiResult<PagedResult<StarterDto>>.Success)startersResult).Value.Items.Where(s => s.IsActive)],
            ((ApiResult<PagedResult<BakeRecordSummaryDto>>.Success)bakesResult).Value.Items,
            (remindersResult as ApiResult<IReadOnlyList<ReminderDto>>.Success)?.Value))));
    }
}
