using Fluxor;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>What the calculator figures depend on: recipe, number of loaves, price date.</summary>
public sealed record CalculatorInput(Guid RecipeId, int Loaves, DateOnly? AsOf);

/// <summary>
/// Recipe detail slice: the recipe and the calculator results, each its own <see cref="Loadable{T}"/> so one failed
/// endpoint does not hide the others. All numbers are API responses (rule 1).
/// </summary>
[FeatureState(Name = "RecipeDetail")]
public sealed record RecipeDetailState(
    Guid? RecipeId,
    Loadable<RecipeDto> Recipe,
    int Loaves,
    DateOnly? AsOf,
    Loadable<ScaledRecipeDto> Scaled,
    Loadable<CostBreakdownDto> Cost,
    Loadable<ProductionFiguresDto> Figures,
    DateTimeOffset? TimelineStart,
    Loadable<TimelineDto> Timeline)
{
    /// <summary>Loaves the cost endpoint accepts.</summary>
    public const int MinLoaves = 1;

    /// <summary>Upper limit of the cost endpoint.</summary>
    public const int MaxLoaves = 1000;

    private RecipeDetailState()
        : this(null, Loadable<RecipeDto>.Idle, 1, null, Loadable<ScaledRecipeDto>.Idle, Loadable<CostBreakdownDto>.Idle,
            Loadable<ProductionFiguresDto>.Idle, null, Loadable<TimelineDto>.Idle)
    {
    }

    /// <summary>The empty state.</summary>
    public static RecipeDetailState Initial { get; } = new();

    /// <summary>Current calculator input, or null before the recipe is open.</summary>
    public CalculatorInput? Input => RecipeId is { } id ? new CalculatorInput(id, Loaves, AsOf) : null;
}

/// <summary>Intent: show a recipe; the timeline starts at <paramref name="TimelineStart"/>.</summary>
public sealed record OpenRecipe(Guid Id, DateTimeOffset TimelineStart);

/// <summary>Result: the recipe arrived.</summary>
public sealed record RecipeLoaded(RecipeDto Recipe);

/// <summary>Result: the recipe failed (404 → not found).</summary>
public sealed record RecipeLoadFailed(Guid Id, ApiError Error);

/// <summary>Intent: calculate for another number of loaves (debounced while the user steps).</summary>
public sealed record ChangeLoaves(int Loaves);

/// <summary>Intent: price the batch as of another date (null = today).</summary>
public sealed record ChangeAsOf(DateOnly? AsOf);

/// <summary>Intent: plan the timeline from another start.</summary>
public sealed record ChangeTimelineStart(DateTimeOffset Start);

/// <summary>Intent: (re)load the calculator figures; <paramref name="CostOnly"/> when only the price date changed.</summary>
public sealed record LoadCalculation(CalculatorInput Input, bool CostOnly);

/// <summary>Result: calculator figures; a null part was not requested and stays as it is.</summary>
public sealed record CalculationLoaded(
    CalculatorInput Input,
    Loadable<ScaledRecipeDto>? Scaled,
    Loadable<CostBreakdownDto> Cost,
    Loadable<ProductionFiguresDto>? Figures);

/// <summary>Intent: load the timeline.</summary>
public sealed record LoadTimeline(Guid RecipeId, DateTimeOffset Start);

/// <summary>Result: timeline or its error.</summary>
public sealed record TimelineLoaded(Guid RecipeId, DateTimeOffset Start, Loadable<TimelineDto> Timeline);

/// <summary>Pure reducers of <see cref="RecipeDetailState"/>.</summary>
public static class RecipeDetailReducers
{
    /// <summary>Another recipe starts empty; reopening the same recipe keeps what is shown while it reloads.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnOpen(RecipeDetailState state, OpenRecipe action) =>
        state.RecipeId == action.Id
            ? state with { Recipe = state.Recipe.Reload() }
            : RecipeDetailState.Initial with
            {
                RecipeId = action.Id,
                Recipe = new Loadable<RecipeDto>.Loading(null),
                TimelineStart = action.TimelineStart,
            };

    /// <summary>The recipe arrived; a first visit starts the calculator at the recipe's own yield.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnRecipeLoaded(RecipeDetailState state, RecipeLoaded action)
    {
        if (state.RecipeId != action.Recipe.Id)
        {
            return state;
        }

        var firstLoad = state.Recipe.Value is null;
        return state with
        {
            Recipe = new Loadable<RecipeDto>.Loaded(action.Recipe),
            Loaves = firstLoad ? action.Recipe.YieldLoaves : state.Loaves,
        };
    }

    /// <summary>The recipe failed.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnRecipeFailed(RecipeDetailState state, RecipeLoadFailed action) =>
        state.RecipeId == action.Id ? state with { Recipe = new Loadable<RecipeDto>.Failed(action.Error) } : state;

    /// <summary>New number of loaves within the endpoint's range; the figures reload.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnChangeLoaves(RecipeDetailState state, ChangeLoaves action) =>
        state with
        {
            Loaves = Math.Clamp(action.Loaves, RecipeDetailState.MinLoaves, RecipeDetailState.MaxLoaves),
            Scaled = state.Scaled.Reload(),
            Cost = state.Cost.Reload(),
            Figures = state.Figures.Reload(),
        };

    /// <summary>New price date; only the cost reloads.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnChangeAsOf(RecipeDetailState state, ChangeAsOf action) =>
        state with { AsOf = action.AsOf, Cost = state.Cost.Reload() };

    /// <summary>New timeline start.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnChangeTimelineStart(RecipeDetailState state, ChangeTimelineStart action) =>
        state with { TimelineStart = action.Start, Timeline = state.Timeline.Reload() };

    /// <summary>Request starts.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnLoadCalculation(RecipeDetailState state, LoadCalculation action) =>
        action.CostOnly
            ? state with { Cost = state.Cost.Reload() }
            : state with { Scaled = state.Scaled.Reload(), Cost = state.Cost.Reload(), Figures = state.Figures.Reload() };

    /// <summary>Results for an older input are dropped (the user kept stepping).</summary>
    [ReducerMethod]
    public static RecipeDetailState OnCalculationLoaded(RecipeDetailState state, CalculationLoaded action) =>
        action.Input == state.Input
            ? state with
            {
                Scaled = action.Scaled ?? state.Scaled,
                Cost = action.Cost,
                Figures = action.Figures ?? state.Figures,
            }
            : state;

    /// <summary>Timeline request starts.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnLoadTimeline(RecipeDetailState state, LoadTimeline action) =>
        state with { Timeline = state.Timeline.Reload() };

    /// <summary>Timeline result for the current recipe and start.</summary>
    [ReducerMethod]
    public static RecipeDetailState OnTimelineLoaded(RecipeDetailState state, TimelineLoaded action) =>
        action.RecipeId == state.RecipeId && action.Start == state.TimelineStart
            ? state with { Timeline = action.Timeline }
            : state;
}
