using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Production.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Production.State;

/// <summary>Recipes and starters to choose from.</summary>
public sealed record NewBatchData(IReadOnlyList<RecipeSummaryDto> Recipes, IReadOnlyList<StarterDto> Starters);

/// <summary>Start window common to all recipes for "ready by" (null when they have none in common).</summary>
public sealed record CommonStartWindow(DateTimeOffset From, DateTimeOffset To);

/// <summary>A preview and, in "ready by" mode, the start window it was planned in.</summary>
public sealed record BatchPreview(BatchDto Batch, CommonStartWindow? Window);

/// <summary>New batch slice (US-20, US-52): reference data, the live preview, the save.</summary>
[FeatureState(Name = "NewBatch")]
public sealed record NewBatchState(Loadable<NewBatchData> Data, Loadable<BatchPreview> Preview, Guid PreviewToken, bool Saving)
{
    private NewBatchState()
        : this(Loadable<NewBatchData>.Idle, Loadable<BatchPreview>.Idle, Guid.Empty, false)
    {
    }
}

/// <summary>Intent: load recipes and starters.</summary>
public sealed record OpenNewBatch;

/// <summary>Result: data.</summary>
public sealed record NewBatchDataLoaded(Loadable<NewBatchData> Data);

/// <summary>
/// Intent: preview the batch. With <paramref name="ReadyAt"/> the start is planned backwards from the recipes' start
/// windows; otherwise <c>Request.PlannedStartAt</c> is used. Debounced; a newer preview replaces an older one.
/// </summary>
public sealed record PreviewBatch(Guid Token, BatchRequest Request, DateTimeOffset? ReadyAt);

/// <summary>Result: preview for <paramref name="Token"/>.</summary>
public sealed record BatchPreviewLoaded(Guid Token, Loadable<BatchPreview> Preview);

/// <summary>Intent: clear the preview (form incomplete).</summary>
public sealed record ClearPreview;

/// <summary>Intent: save the batch.</summary>
public sealed record CreateBatch(BatchRequest Request);

/// <summary>Result: saving failed.</summary>
public sealed record BatchCreateFailed(ApiError Error);

/// <summary>Reducers of <see cref="NewBatchState"/>.</summary>
public static class NewBatchReducers
{
    /// <summary>Opening starts with an empty preview.</summary>
    [ReducerMethod(typeof(OpenNewBatch))]
    public static NewBatchState OnOpen(NewBatchState state) =>
        new(state.Data.Reload(), Loadable<BatchPreview>.Idle, Guid.Empty, false);

    /// <summary>Data loaded.</summary>
    [ReducerMethod]
    public static NewBatchState OnData(NewBatchState state, NewBatchDataLoaded action) => state with { Data = action.Data };

    /// <summary>Preview requested; the current preview stays until the new one arrives.</summary>
    [ReducerMethod]
    public static NewBatchState OnPreview(NewBatchState state, PreviewBatch action) =>
        state with { PreviewToken = action.Token, Preview = state.Preview.Reload() };

    /// <summary>Only the newest preview is kept.</summary>
    [ReducerMethod]
    public static NewBatchState OnPreviewLoaded(NewBatchState state, BatchPreviewLoaded action) =>
        action.Token == state.PreviewToken ? state with { Preview = action.Preview } : state;

    /// <summary>Preview cleared.</summary>
    [ReducerMethod(typeof(ClearPreview))]
    public static NewBatchState OnClear(NewBatchState state) => state with { PreviewToken = Guid.NewGuid(), Preview = Loadable<BatchPreview>.Idle };

    /// <summary>Saving.</summary>
    [ReducerMethod(typeof(CreateBatch))]
    public static NewBatchState OnCreate(NewBatchState state) => state with { Saving = true };

    /// <summary>Saved.</summary>
    [ReducerMethod(typeof(BatchCreated))]
    public static NewBatchState OnCreated(NewBatchState state) => state with { Saving = false, Preview = Loadable<BatchPreview>.Idle };

    /// <summary>Save failed.</summary>
    [ReducerMethod(typeof(BatchCreateFailed))]
    public static NewBatchState OnCreateFailed(NewBatchState state) => state with { Saving = false };
}

/// <summary>Side effects of the new batch form.</summary>
public sealed class NewBatchEffects(IProductionApi api, TimeProvider time, NavigationManager navigation) : IDisposable
{
    /// <summary>Pause after the last change before the preview is requested (US-20: preview on every change).</summary>
    public static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(400);

    private CancellationTokenSource? _pending;

    /// <summary>Loads recipes (not archived) and starters.</summary>
    [EffectMethod(typeof(OpenNewBatch))]
    public async Task HandleOpen(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        var recipes = api.ListRecipesAsync();
        var starters = api.ListStartersAsync();
        var recipesResult = await recipes;
        var startersResult = await starters;
        if (recipesResult is ApiResult<PagedResult<RecipeSummaryDto>>.Failure { Error: var error })
        {
            dispatcher.Dispatch(new NewBatchDataLoaded(new Loadable<NewBatchData>.Failed(error)));
            return;
        }

        var list = ((ApiResult<PagedResult<RecipeSummaryDto>>.Success)recipesResult).Value.Items;
        var starterList = startersResult is ApiResult<PagedResult<StarterDto>>.Success { Value: var s } ? s.Items : [];
        dispatcher.Dispatch(new NewBatchDataLoaded(new Loadable<NewBatchData>.Loaded(new NewBatchData(list, starterList))));
    }

    /// <summary>Debounces, then plans backwards when needed and asks the API for the preview.</summary>
    [EffectMethod]
    public async Task HandlePreview(PreviewBatch action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        try
        {
            await Task.Delay(PreviewDelay, time, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        var request = action.Request;
        CommonStartWindow? window = null;
        if (action.ReadyAt is { } readyAt)
        {
            var (found, error) = await CommonWindowAsync(request.Items.Select(i => i.RecipeId).Distinct(), readyAt, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            if (error is not null)
            {
                dispatcher.Dispatch(new BatchPreviewLoaded(action.Token, new Loadable<BatchPreview>.Failed(error)));
                return;
            }

            if (found is null)
            {
                dispatcher.Dispatch(new BatchPreviewLoaded(action.Token, new Loadable<BatchPreview>.Failed(new ApiError(
                    ApiErrorKind.BusinessRule, null, "No common start time",
                    "These recipes cannot all be ready at that time with one start. Plan them as separate batches or pick another time.",
                    new Dictionary<string, string[]>(), null, null))));
                return;
            }

            window = found;
            // Latest common start: everything is ready in time and the dough waits least (screens-and-routes.md US-52).
            request = request with { PlannedStartAt = found.To };
        }

        var result = await api.PreviewAsync(request, token);
        if (!token.IsCancellationRequested)
        {
            dispatcher.Dispatch(new BatchPreviewLoaded(action.Token, result switch
            {
                ApiResult<BatchDto>.Success { Value: var batch } => new Loadable<BatchPreview>.Loaded(new BatchPreview(batch, window)),
                ApiResult<BatchDto>.Failure { Error: var e } => new Loadable<BatchPreview>.Failed(e),
                _ => throw new InvalidOperationException(),
            }));
        }
    }

    /// <summary>Saves, announces <see cref="BatchCreated"/> and opens the batch.</summary>
    [EffectMethod]
    public async Task HandleCreate(CreateBatch action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.CreateBatchAsync(action.Request))
        {
            case ApiResult<BatchDto>.Success { Value: var batch }:
                dispatcher.Dispatch(new BatchCreated(batch.Id, batch.Name));
                dispatcher.Dispatch(new ShowToast(Toast.Success("Batch saved")));
                navigation.NavigateTo($"batches/{batch.Id}");
                break;
            case ApiResult<BatchDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new BatchCreateFailed(error));
                break;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _pending?.Dispose();

    /// <summary>Intersection of the recipes' start windows: the latest "from" and the earliest "to"; null when empty.</summary>
    private async Task<(CommonStartWindow? Window, ApiError? Error)> CommonWindowAsync(
        IEnumerable<Guid> recipeIds, DateTimeOffset readyAt, CancellationToken ct)
    {
        var results = await Task.WhenAll(recipeIds.Select(id => api.GetStartWindowAsync(id, readyAt, ct)));
        if (results.OfType<ApiResult<StartWindowDto>.Failure>().FirstOrDefault() is { } failed)
        {
            return (null, failed.Error);
        }

        var windows = results.OfType<ApiResult<StartWindowDto>.Success>().Select(s => s.Value).ToList();
        var from = windows.Max(w => w.StartFrom);
        var to = windows.Min(w => w.StartTo);
        return (from <= to ? new CommonStartWindow(from, to) : null, null);
    }
}
