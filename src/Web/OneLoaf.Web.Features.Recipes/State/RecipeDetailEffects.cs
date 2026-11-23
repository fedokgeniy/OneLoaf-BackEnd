using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>
/// Side effects of the recipe detail: recipe, then scaled quantities + cost + production figures in parallel, timeline.
/// Stepping the loaves is debounced so a burst of clicks sends one request per endpoint.
/// </summary>
public sealed class RecipeDetailEffects(IRecipesApi api, IState<RecipeDetailState> state, TimeProvider time) : IDisposable
{
    /// <summary>Pause after the last step of the loaves stepper.</summary>
    public static readonly TimeSpan StepDelay = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _pendingStep;

    /// <summary>Loads the recipe; on success the calculator and the timeline follow.</summary>
    [EffectMethod]
    public async Task HandleOpen(OpenRecipe action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = await api.GetAsync(action.Id);
        switch (result)
        {
            case ApiResult<RecipeDto>.Success { Value: var recipe }:
                dispatcher.Dispatch(new RecipeLoaded(recipe));
                if (state.Value.Input is { } input)
                {
                    dispatcher.Dispatch(new LoadCalculation(input, CostOnly: false));
                }

                if (state.Value.TimelineStart is { } start)
                {
                    dispatcher.Dispatch(new LoadTimeline(recipe.Id, start));
                }

                break;
            case ApiResult<RecipeDto>.Failure { Error: var error }:
                dispatcher.Dispatch(new RecipeLoadFailed(action.Id, error));
                break;
        }
    }

    /// <summary>Waits until the user stops stepping, then recalculates.</summary>
    [EffectMethod(typeof(ChangeLoaves))]
    public async Task HandleChangeLoaves(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        _pendingStep?.Cancel();
        _pendingStep?.Dispose();
        _pendingStep = new CancellationTokenSource();
        try
        {
            await Task.Delay(StepDelay, time, _pendingStep.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (state.Value.Input is { } input)
        {
            dispatcher.Dispatch(new LoadCalculation(input, CostOnly: false));
        }
    }

    /// <summary>Price date changed: only the cost depends on it.</summary>
    [EffectMethod(typeof(ChangeAsOf))]
    public Task HandleChangeAsOf(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.Input is { } input)
        {
            dispatcher.Dispatch(new LoadCalculation(input, CostOnly: true));
        }

        return Task.CompletedTask;
    }

    /// <summary>Timeline start changed.</summary>
    [EffectMethod]
    public Task HandleChangeTimelineStart(ChangeTimelineStart action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (state.Value.RecipeId is { } id)
        {
            dispatcher.Dispatch(new LoadTimeline(id, action.Start));
        }

        return Task.CompletedTask;
    }

    /// <summary>Calls the calculator endpoints in parallel.</summary>
    [EffectMethod]
    public async Task HandleLoadCalculation(LoadCalculation action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var (id, loaves, asOf) = action.Input;
        var cost = api.GetCostAsync(id, loaves, asOf);
        if (action.CostOnly)
        {
            dispatcher.Dispatch(new CalculationLoaded(action.Input, null, (await cost).ToLoadable(), null));
            return;
        }

        var scaled = api.ScaleAsync(id, loaves);
        var figures = api.GetProductionFiguresAsync(id, loaves);
        await Task.WhenAll(cost, scaled, figures);
        dispatcher.Dispatch(new CalculationLoaded(
            action.Input, (await scaled).ToLoadable(), (await cost).ToLoadable(), (await figures).ToLoadable()));
    }

    /// <summary>Loads the timeline.</summary>
    [EffectMethod]
    public async Task HandleLoadTimeline(LoadTimeline action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = await api.GetTimelineAsync(action.RecipeId, action.Start);
        dispatcher.Dispatch(new TimelineLoaded(action.RecipeId, action.Start, result.ToLoadable()));
    }

    /// <inheritdoc />
    public void Dispose() => _pendingStep?.Dispose();
}
