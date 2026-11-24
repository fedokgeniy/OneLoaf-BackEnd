using Fluxor;
using OneLoaf.Web.Features.Production.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Production.State;

/// <summary>What a stage command does.</summary>
public enum StageCommandKind
{
    /// <summary>Start (US-23).</summary>
    Start,

    /// <summary>Complete (US-23).</summary>
    Complete,

    /// <summary>Skip an optional stage.</summary>
    Skip,

    /// <summary>+30 minutes (US-24).</summary>
    Extend,

    /// <summary>New planned durations (US-21).</summary>
    Adjust,

    /// <summary>Back to the recipe's durations (US-21).</summary>
    Reset,
}

/// <summary>Batch execution slice (US-21–25, US-31).</summary>
[FeatureState(Name = "Batch")]
public sealed record BatchState(Guid? BatchId, Loadable<BatchDto> Batch, Loadable<KneadingPlanDto> Plan, Guid? BusyStageId, bool BusyBatch)
{
    private BatchState()
        : this(null, Loadable<BatchDto>.Idle, Loadable<KneadingPlanDto>.Idle, null, false)
    {
    }
}

/// <summary>Intent: open a batch.</summary>
public sealed record OpenBatch(Guid Id);

/// <summary>Result: batch (and kneading plan) loaded.</summary>
public sealed record BatchLoaded(Guid Id, Loadable<BatchDto> Batch, Loadable<KneadingPlanDto> Plan);

/// <summary>Intent: run a stage command; <paramref name="Durations"/> only for <see cref="StageCommandKind.Adjust"/>.</summary>
public sealed record RunStageCommand(Guid BatchId, Guid StageId, StageCommandKind Kind, StageDurationRequest? Durations = null);

/// <summary>Result: the API returned the updated batch.</summary>
public sealed record StageCommandDone(BatchDto Batch);

/// <summary>Result: a batch or stage command failed.</summary>
public sealed record BatchCommandFailed(ApiError Error);

/// <summary>Intent: complete the whole batch (US-31).</summary>
public sealed record CompleteBatch(Guid Id);

/// <summary>Intent: cancel the batch.</summary>
public sealed record CancelBatch(Guid Id);

/// <summary>Result: cancelled.</summary>
public sealed record BatchCancelled(Guid Id);

/// <summary>Intent: save the actual durations as a variant of the recipe (US-25).</summary>
public sealed record SaveBatchAsVariant(Guid BatchId, Guid? ItemId, string? Title);

/// <summary>Result: variant created.</summary>
public sealed record BatchSavedAsVariant(SaveAsVariantResponse Response);

/// <summary>Reducers of <see cref="BatchState"/>.</summary>
public static class BatchReducers
{
    /// <summary>Opening; the same batch keeps showing while it reloads.</summary>
    [ReducerMethod]
    public static BatchState OnOpen(BatchState state, OpenBatch action) =>
        state.BatchId == action.Id
            ? state with { Batch = state.Batch.Reload() }
            : new BatchState(action.Id, new Loadable<BatchDto>.Loading(null), Loadable<KneadingPlanDto>.Idle, null, false);

    /// <summary>Loaded.</summary>
    [ReducerMethod]
    public static BatchState OnLoaded(BatchState state, BatchLoaded action) =>
        action.Id == state.BatchId ? state with { Batch = action.Batch, Plan = action.Plan } : state;

    /// <summary>The stage is busy until the API answers (no double clicks).</summary>
    [ReducerMethod]
    public static BatchState OnStageCommand(BatchState state, RunStageCommand action) => state with { BusyStageId = action.StageId };

    /// <summary>The API's batch replaces the shown one.</summary>
    [ReducerMethod]
    public static BatchState OnStageDone(BatchState state, StageCommandDone action) =>
        action.Batch.Id == state.BatchId ? state with { Batch = new Loadable<BatchDto>.Loaded(action.Batch), BusyStageId = null } : state;

    /// <summary>A command failed.</summary>
    [ReducerMethod(typeof(BatchCommandFailed))]
    public static BatchState OnFailed(BatchState state) => state with { BusyStageId = null, BusyBatch = false };

    /// <summary>Batch command runs.</summary>
    [ReducerMethod(typeof(CompleteBatch))]
    public static BatchState OnComplete(BatchState state) => state with { BusyBatch = true };

    /// <summary>Batch command runs.</summary>
    [ReducerMethod(typeof(CancelBatch))]
    public static BatchState OnCancel(BatchState state) => state with { BusyBatch = true };

    /// <summary>Batch command runs.</summary>
    [ReducerMethod(typeof(SaveBatchAsVariant))]
    public static BatchState OnSaveAsVariant(BatchState state) => state with { BusyBatch = true };

    /// <summary>The batch reloads after completion.</summary>
    [ReducerMethod(typeof(BatchCompleted))]
    public static BatchState OnCompleted(BatchState state) => state with { BusyBatch = false };

    /// <summary>The batch reloads after cancel.</summary>
    [ReducerMethod(typeof(BatchCancelled))]
    public static BatchState OnCancelled(BatchState state) => state with { BusyBatch = false };

    /// <summary>Variant saved.</summary>
    [ReducerMethod(typeof(BatchSavedAsVariant))]
    public static BatchState OnVariant(BatchState state) => state with { BusyBatch = false };
}

/// <summary>Side effects of batch execution.</summary>
public sealed class BatchEffects(IProductionApi api)
{
    /// <summary>Loads the batch and its kneading plan in parallel.</summary>
    [EffectMethod]
    public async Task HandleOpen(OpenBatch action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var batch = api.GetBatchAsync(action.Id);
        var plan = api.GetKneadingPlanAsync(action.Id);
        dispatcher.Dispatch(new BatchLoaded(action.Id, (await batch).ToLoadable(), (await plan).ToLoadable()));
    }

    /// <summary>Runs the stage command; a 409 (stage already running, wrong state) reloads the batch.</summary>
    [EffectMethod]
    public async Task HandleStageCommand(RunStageCommand action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var none = new StageActionRequest(null, null);
        var result = action.Kind switch
        {
            StageCommandKind.Start => await api.StageActionAsync(action.BatchId, action.StageId, "start", none),
            StageCommandKind.Complete => await api.StageActionAsync(action.BatchId, action.StageId, "complete", none),
            StageCommandKind.Skip => await api.StageActionAsync(action.BatchId, action.StageId, "skip", none),
            StageCommandKind.Extend => await api.ExtendStageAsync(action.BatchId, action.StageId, 30),
            StageCommandKind.Adjust => await api.ChangeDurationsAsync(action.BatchId, action.StageId, action.Durations!),
            StageCommandKind.Reset => await api.ResetStageAsync(action.BatchId, action.StageId),
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
        switch (result)
        {
            case ApiResult<BatchDto>.Success { Value: var batch }:
                dispatcher.Dispatch(new StageCommandDone(batch));
                break;
            case ApiResult<BatchDto>.Failure { Error: var error }:
                Fail(error, dispatcher, reloadBatchId: error.Kind == ApiErrorKind.Conflict ? action.BatchId : null);
                break;
        }
    }

    /// <summary>Completes the batch; the bake record is created by the API (US-31).</summary>
    [EffectMethod]
    public async Task HandleComplete(CompleteBatch action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.CompleteBatchAsync(action.Id))
        {
            case ApiResult<CompleteBatchResponse>.Success { Value: var done }:
                dispatcher.Dispatch(new BatchCompleted(action.Id, done.BakeRecordId));
                dispatcher.Dispatch(new ShowToast(done.BakeRecordId is { } record
                    ? Toast.Success("Batch completed — rate it in your history", "Rate the bake", $"history/{record}")
                    : Toast.Success("Batch completed")));
                dispatcher.Dispatch(new OpenBatch(action.Id));
                break;
            case ApiResult<CompleteBatchResponse>.Failure { Error: var error }:
                Fail(error, dispatcher, reloadBatchId: null);
                break;
        }
    }

    /// <summary>Cancels the batch.</summary>
    [EffectMethod]
    public async Task HandleCancel(CancelBatch action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.CancelBatchAsync(action.Id))
        {
            case ApiResult.Failure { Error: var error }:
                Fail(error, dispatcher, reloadBatchId: null);
                break;
            default:
                dispatcher.Dispatch(new BatchCancelled(action.Id));
                dispatcher.Dispatch(new ShowToast(Toast.Info("Batch cancelled")));
                dispatcher.Dispatch(new OpenBatch(action.Id));
                break;
        }
    }

    /// <summary>Saves the actual durations as a recipe variant (US-25).</summary>
    [EffectMethod]
    public async Task HandleSaveAsVariant(SaveBatchAsVariant action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        switch (await api.SaveAsVariantAsync(action.BatchId, new SaveAsVariantRequest(action.ItemId, action.Title)))
        {
            case ApiResult<SaveAsVariantResponse>.Success { Value: var response }:
                dispatcher.Dispatch(new BatchSavedAsVariant(response));
                dispatcher.Dispatch(new ShowToast(Toast.Success($"Variant “{response.Recipe.Title}” saved", "Open recipe", $"recipes/{response.Recipe.Id}")));
                break;
            case ApiResult<SaveAsVariantResponse>.Failure { Error: var error }:
                Fail(error, dispatcher, reloadBatchId: null);
                break;
        }
    }

    private static void Fail(ApiError error, IDispatcher dispatcher, Guid? reloadBatchId)
    {
        dispatcher.Dispatch(new BatchCommandFailed(error));
        dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
        if (reloadBatchId is { } id)
        {
            dispatcher.Dispatch(new OpenBatch(id));
        }
    }
}
