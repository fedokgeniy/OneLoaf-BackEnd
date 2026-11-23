using Fluxor;
using Microsoft.Extensions.Time.Testing;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Features.Recipes.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class RecipeDetailStateTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 8, 0, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Reducers_FirstLoad_StartAtRecipeYieldAndDropStaleResults()
    {
        var state = RecipeDetailReducers.OnOpen(RecipeDetailState.Initial, new OpenRecipe(Id, Start));
        state = RecipeDetailReducers.OnRecipeLoaded(state, new RecipeLoaded(Recipe(yieldLoaves: 2)));
        Assert.Equal(2, state.Loaves);

        state = RecipeDetailReducers.OnChangeLoaves(state, new ChangeLoaves(5000));
        Assert.Equal(RecipeDetailState.MaxLoaves, state.Loaves);

        var stale = RecipeDetailReducers.OnCalculationLoaded(state,
            new CalculationLoaded(new CalculatorInput(Id, 2, null), null, Failed<CostBreakdownDto>(), null));
        Assert.IsType<Loadable<CostBreakdownDto>.Loading>(stale.Cost);

        var fresh = RecipeDetailReducers.OnCalculationLoaded(state,
            new CalculationLoaded(state.Input!, null, Failed<CostBreakdownDto>(), null));
        Assert.IsType<Loadable<CostBreakdownDto>.Failed>(fresh.Cost);
        Assert.IsType<Loadable<ScaledRecipeDto>.Loading>(fresh.Scaled);
    }

    [Fact]
    public async Task HandleOpen_RecipeFound_LoadsCalculatorAndTimeline()
    {
        var store = new StubState(RecipeDetailReducers.OnOpen(RecipeDetailState.Initial, new OpenRecipe(Id, Start)));
        var dispatcher = new ReducingDispatcher(store);
        using var effects = new RecipeDetailEffects(new StubApi(), store, TimeProvider.System);

        await effects.HandleOpen(new OpenRecipe(Id, Start), dispatcher);
        await effects.HandleLoadCalculation(dispatcher.Actions.OfType<LoadCalculation>().Single(), dispatcher);

        Assert.Equal(new CalculatorInput(Id, 3, null), dispatcher.Actions.OfType<LoadCalculation>().Single().Input);
        Assert.Equal(Start, dispatcher.Actions.OfType<LoadTimeline>().Single().Start);
        Assert.Equal(2880m, Assert.IsType<Loadable<ProductionFiguresDto>.Loaded>(store.Value.Figures).Data.DoughWeightG);
        Assert.Equal(6.05m, Assert.IsType<Loadable<CostBreakdownDto>.Loaded>(store.Value.Cost).Data.Total);
    }

    [Fact]
    public async Task HandleChangeLoaves_QuickSteps_RecalculatesOnce()
    {
        var time = new FakeTimeProvider();
        var opened = RecipeDetailReducers.OnRecipeLoaded(
            RecipeDetailReducers.OnOpen(RecipeDetailState.Initial, new OpenRecipe(Id, Start)), new RecipeLoaded(Recipe(1)));
        var store = new StubState(opened with { Loaves = 4 });
        var dispatcher = new ReducingDispatcher(store);
        using var effects = new RecipeDetailEffects(new StubApi(), store, time);

        var first = effects.HandleChangeLoaves(dispatcher);
        var second = effects.HandleChangeLoaves(dispatcher);
        time.Advance(RecipeDetailEffects.StepDelay);
        await Task.WhenAll(first, second);

        Assert.Equal(4, Assert.Single(dispatcher.Actions.OfType<LoadCalculation>()).Input.Loaves);
    }

    private static RecipeDto Recipe(int yieldLoaves) => new(Id, null, "Country sourdough", null, yieldLoaves, 900m, null, false,
        [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static Loadable<T> Failed<T>() => new Loadable<T>.Failed(ApiError.FromStatus(500));

    private sealed class StubApi : IRecipesApi
    {
        public Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListAsync(RecipeQuery query, int page, CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<ApiResult<RecipeDto>> GetAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<RecipeDto>.Ok(Recipe(3)));

        public Task<ApiResult<ScaledRecipeDto>> ScaleAsync(Guid id, int loaves, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<ScaledRecipeDto>.Ok(new ScaledRecipeDto(id, 1, loaves, [])));

        public Task<ApiResult<CostBreakdownDto>> GetCostAsync(Guid id, int loaves, DateOnly? asOf, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<CostBreakdownDto>.Ok(new CostBreakdownDto(id, loaves, new DateOnly(2026, 11, 23), "EUR", true, [], 6.05m, [])));

        public Task<ApiResult<ProductionFiguresDto>> GetProductionFiguresAsync(Guid id, int loaves, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<ProductionFiguresDto>.Ok(new ProductionFiguresDto(id, loaves, 2880m, 1, 300m)));

        public Task<ApiResult<TimelineDto>> GetTimelineAsync(Guid id, DateTimeOffset start, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<TimelineDto>.Ok(new TimelineDto(id, start, [], start, start)));
    }

    private sealed class StubState(RecipeDetailState value) : IState<RecipeDetailState>
    {
        public event EventHandler? StateChanged;

        public RecipeDetailState Value { get; set; } = value;

        public void Raise() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Applies the reducers like the store, so effects that read state after dispatching see the new state.</summary>
    private sealed class ReducingDispatcher(StubState store) : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public List<object> Actions { get; } = [];

        public void Dispatch(object action)
        {
            Actions.Add(action);
            store.Value = action switch
            {
                RecipeLoaded a => RecipeDetailReducers.OnRecipeLoaded(store.Value, a),
                LoadCalculation a => RecipeDetailReducers.OnLoadCalculation(store.Value, a),
                CalculationLoaded a => RecipeDetailReducers.OnCalculationLoaded(store.Value, a),
                LoadTimeline a => RecipeDetailReducers.OnLoadTimeline(store.Value, a),
                _ => store.Value,
            };
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }
}
