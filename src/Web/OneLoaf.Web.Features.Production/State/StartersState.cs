using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Production.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Production.State;

/// <summary>Starters slice (US-27–30): starters, the selected starter's feeding log.</summary>
[FeatureState(Name = "Starters")]
public sealed record StartersState(
    Loadable<IReadOnlyList<StarterDto>> Starters,
    Loadable<IReadOnlyList<FlourDto>> Flours,
    Guid? SelectedId,
    Loadable<IReadOnlyList<FeedingDto>> Feedings,
    bool Busy)
{
    private StartersState()
        : this(Loadable<IReadOnlyList<StarterDto>>.Idle, Loadable<IReadOnlyList<FlourDto>>.Idle, null, Loadable<IReadOnlyList<FeedingDto>>.Idle, false)
    {
    }
}

/// <summary>Intent: load the starters (and flours for the form).</summary>
public sealed record LoadStarters;

/// <summary>Result: starters.</summary>
public sealed record StartersLoaded(Loadable<IReadOnlyList<StarterDto>> Starters, Loadable<IReadOnlyList<FlourDto>> Flours);

/// <summary>Intent: show a starter's feeding log.</summary>
public sealed record SelectStarter(Guid? Id);

/// <summary>Result: feeding log.</summary>
public sealed record FeedingsLoaded(Guid Id, Loadable<IReadOnlyList<FeedingDto>> Feedings);

/// <summary>Intent: log a feeding (US-28).</summary>
public sealed record LogFeeding(Guid StarterId, FeedingRequest Request);

/// <summary>Result: feeding logged.</summary>
public sealed record FeedingLogged(Guid StarterId, FeedingDto Feeding);

/// <summary>Intent: create (null id) or update a starter.</summary>
public sealed record SaveStarter(Guid? Id, StarterRequest Request);

/// <summary>Result: saved.</summary>
public sealed record StarterSaved(StarterDto Starter);

/// <summary>Intent: delete a starter.</summary>
public sealed record DeleteStarter(Guid Id);

/// <summary>Result: deleted.</summary>
public sealed record StarterDeleted(Guid Id);

/// <summary>Result: a starter command failed.</summary>
public sealed record StarterCommandFailed(string Command, ApiError Error);

/// <summary>Reducers of <see cref="StartersState"/>.</summary>
public static class StartersReducers
{
    /// <summary>Loading.</summary>
    [ReducerMethod(typeof(LoadStarters))]
    public static StartersState OnLoad(StartersState state) => state with { Starters = state.Starters.Reload() };

    /// <summary>Loaded.</summary>
    [ReducerMethod]
    public static StartersState OnLoaded(StartersState state, StartersLoaded action) =>
        state with { Starters = action.Starters, Flours = action.Flours };

    /// <summary>Selection.</summary>
    [ReducerMethod]
    public static StartersState OnSelect(StartersState state, SelectStarter action) =>
        state with
        {
            SelectedId = action.Id,
            Feedings = action.Id is null ? Loadable<IReadOnlyList<FeedingDto>>.Idle
                : action.Id == state.SelectedId ? state.Feedings.Reload() : new Loadable<IReadOnlyList<FeedingDto>>.Loading(null),
        };

    /// <summary>Feedings of the selected starter.</summary>
    [ReducerMethod]
    public static StartersState OnFeedings(StartersState state, FeedingsLoaded action) =>
        action.Id == state.SelectedId ? state with { Feedings = action.Feedings } : state;

    /// <summary>Busy.</summary>
    [ReducerMethod(typeof(LogFeeding))]
    public static StartersState OnLog(StartersState state) => state with { Busy = true };

    /// <summary>Busy.</summary>
    [ReducerMethod(typeof(SaveStarter))]
    public static StartersState OnSave(StartersState state) => state with { Busy = true };

    /// <summary>Busy.</summary>
    [ReducerMethod(typeof(DeleteStarter))]
    public static StartersState OnDelete(StartersState state) => state with { Busy = true };

    /// <summary>Done.</summary>
    [ReducerMethod(typeof(FeedingLogged))]
    public static StartersState OnLogged(StartersState state) => state with { Busy = false };

    /// <summary>Done; the list reloads.</summary>
    [ReducerMethod(typeof(StarterSaved))]
    public static StartersState OnSaved(StartersState state) => state with { Busy = false, Starters = Loadable<IReadOnlyList<StarterDto>>.Idle };

    /// <summary>Deleted; the list reloads.</summary>
    [ReducerMethod]
    public static StartersState OnDeleted(StartersState state, StarterDeleted action) =>
        state with
        {
            Busy = false,
            Starters = Loadable<IReadOnlyList<StarterDto>>.Idle,
            SelectedId = state.SelectedId == action.Id ? null : state.SelectedId,
        };

    /// <summary>Failed.</summary>
    [ReducerMethod(typeof(StarterCommandFailed))]
    public static StartersState OnFailed(StartersState state) => state with { Busy = false };
}

/// <summary>Side effects of the starters slice.</summary>
public sealed class StartersEffects(IProductionApi api, NavigationManager navigation)
{
    /// <summary>Loads starters and flours in parallel; flours are optional (form select only).</summary>
    [EffectMethod(typeof(LoadStarters))]
    public async Task HandleLoad(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        var starters = api.ListStartersAsync();
        var flours = api.ListFloursAsync();
        var startersResult = await starters;
        var floursResult = await flours;
        dispatcher.Dispatch(new StartersLoaded(
            startersResult switch
            {
                ApiResult<PagedResult<StarterDto>>.Success { Value: var page } => new Loadable<IReadOnlyList<StarterDto>>.Loaded(page.Items),
                ApiResult<PagedResult<StarterDto>>.Failure { Error: var error } => new Loadable<IReadOnlyList<StarterDto>>.Failed(error),
                _ => throw new InvalidOperationException(),
            },
            floursResult is ApiResult<PagedResult<FlourDto>>.Success { Value: var f }
                ? new Loadable<IReadOnlyList<FlourDto>>.Loaded(f.Items)
                : new Loadable<IReadOnlyList<FlourDto>>.Loaded([])));
    }

    /// <summary>Loads the feeding log.</summary>
    [EffectMethod]
    public async Task HandleSelect(SelectStarter action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (action.Id is { } id)
        {
            var result = await api.ListFeedingsAsync(id);
            dispatcher.Dispatch(new FeedingsLoaded(id, result switch
            {
                ApiResult<PagedResult<FeedingDto>>.Success { Value: var page } => new Loadable<IReadOnlyList<FeedingDto>>.Loaded(page.Items),
                ApiResult<PagedResult<FeedingDto>>.Failure { Error: var error } => new Loadable<IReadOnlyList<FeedingDto>>.Failed(error),
                _ => throw new InvalidOperationException(),
            }));
        }
    }

    /// <summary>Logs a feeding; the next feeding and peak come back from the API.</summary>
    [EffectMethod]
    public async Task HandleLog(LogFeeding action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.AddFeedingAsync(action.StarterId, action.Request))
        {
            case ApiResult<FeedingDto>.Success { Value: var feeding }:
                dispatcher.Dispatch(new FeedingLogged(action.StarterId, feeding));
                dispatcher.Dispatch(new ShowToast(Toast.Success(feeding.NextFeedingAt is { } next
                    ? $"Feeding logged — next feeding {next.ToLocalTime():ddd HH:mm}"
                    : "Feeding logged")));
                dispatcher.Dispatch(new LoadStarters());
                dispatcher.Dispatch(new SelectStarter(action.StarterId));
                break;
            case ApiResult<FeedingDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new StarterCommandFailed(nameof(LogFeeding), error));
                break;
        }
    }

    /// <summary>Saves and returns to the list.</summary>
    [EffectMethod]
    public async Task HandleSave(SaveStarter action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = action.Id is { } id ? await api.UpdateStarterAsync(id, action.Request) : await api.CreateStarterAsync(action.Request);
        switch (result)
        {
            case ApiResult<StarterDto>.Success { Value: var starter }:
                dispatcher.Dispatch(new StarterSaved(starter));
                dispatcher.Dispatch(new ShowToast(Toast.Success(action.Id is null ? "Starter added" : "Starter saved")));
                navigation.NavigateTo("starters");
                break;
            case ApiResult<StarterDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new StarterCommandFailed(nameof(SaveStarter), error));
                break;
        }
    }

    /// <summary>Deletes and returns to the list.</summary>
    [EffectMethod]
    public async Task HandleDelete(DeleteStarter action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.DeleteStarterAsync(action.Id))
        {
            case ApiResult.Failure { Error: var error }:
                dispatcher.Dispatch(new StarterCommandFailed(nameof(DeleteStarter), error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
            default:
                dispatcher.Dispatch(new StarterDeleted(action.Id));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Starter deleted")));
                navigation.NavigateTo("starters");
                break;
        }
    }
}
