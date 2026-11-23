using Fluxor;
using Microsoft.Extensions.Time.Testing;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Features.Recipes.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class RecipesStateTests
{
    private static readonly RecipeSummaryDto Country = new(Guid.NewGuid(), "Country sourdough", null, 1, false, DateTimeOffset.UnixEpoch);

    [Fact]
    public void OnLoaded_ResponseForOlderQuery_IsDropped()
    {
        var rye = new RecipeQuery("rye", false, null);
        var state = RecipesReducers.OnSetQuery(Initial(), new SetRecipeQuery(rye, Debounce: true));

        var stale = RecipesReducers.OnLoaded(state, new RecipesLoaded(new RecipeQuery("r", false, null), List(Country)));
        var fresh = RecipesReducers.OnLoaded(stale, new RecipesLoaded(rye, List(Country)));

        Assert.IsType<Loadable<RecipeList>.Loading>(stale.List);
        Assert.Single(Assert.IsType<Loadable<RecipeList>.Loaded>(fresh.List).Data.Items);
    }

    [Fact]
    public void OnMoreLoaded_NextPage_IsAppended()
    {
        var variant = Country with { Id = Guid.NewGuid(), Title = "Seeded country", ParentRecipeId = Country.Id };
        var state = RecipesReducers.OnLoaded(Initial(), new RecipesLoaded(RecipeQuery.None, List(Country) with { HasMore = true }));
        state = RecipesReducers.OnLoadMore(state);

        state = RecipesReducers.OnMoreLoaded(state, new MoreRecipesLoaded(RecipeQuery.None, List(variant) with { Page = 2 }));

        var list = Assert.IsType<Loadable<RecipeList>.Loaded>(state.List).Data;
        Assert.Equal(["Country sourdough", "Seeded country"], list.Items.Select(r => r.Title));
        Assert.Equal(2, list.Page);
        Assert.False(state.LoadingMore);
        var cards = RecipesSelectors.VisibleCards(state with { VariantsOnly = true });
        Assert.Equal("Country sourdough", Assert.Single(cards).ParentTitle);
    }

    [Fact]
    public async Task HandleSetQuery_TypingQuickly_LoadsOnlyTheLastQuery()
    {
        var time = new FakeTimeProvider();
        var dispatcher = new RecordingDispatcher();
        using var effects = new RecipesEffects(new StubApi(), new StubState(Initial()), time);

        var first = effects.HandleSetQuery(new SetRecipeQuery(new RecipeQuery("r", false, null), true), dispatcher);
        var second = effects.HandleSetQuery(new SetRecipeQuery(new RecipeQuery("rye", false, null), true), dispatcher);
        time.Advance(RecipesEffects.SearchDelay);
        await Task.WhenAll(first, second);

        Assert.Equal("rye", Assert.IsType<LoadRecipes>(Assert.Single(dispatcher.Actions)).Query.Search);
    }

    [Fact]
    public async Task HandleLoad_ApiFails_DispatchesFailureForTheQuery()
    {
        var dispatcher = new RecordingDispatcher();
        var error = ApiError.FromStatus(500, "c1");
        using var effects = new RecipesEffects(new StubApi(error), new StubState(Initial()), TimeProvider.System);

        await effects.HandleLoad(new LoadRecipes(RecipeQuery.None), dispatcher);

        var failed = Assert.IsType<RecipesLoadFailed>(Assert.Single(dispatcher.Actions));
        Assert.Same(error, failed.Error);
    }

    [Fact]
    public void Build_Query_EscapesAndSkipsEmptyValues() =>
        Assert.Equal("api/v1/recipes?search=rye%20%26%20spelt&minProteinPercent=12.5&page=2",
            Url.Build("api/v1/recipes", ("search", " rye & spelt "), ("includeArchived", null), ("minProteinPercent", 12.5m), ("page", 2)));

    private static RecipesState Initial() =>
        new(RecipeQuery.None, false, Loadable<RecipeList>.Idle, false);

    private static RecipeList List(params RecipeSummaryDto[] items) => new([.. items], 1, items.Length, false);

    private sealed class StubApi(ApiError? error = null) : RecipesApiStub
    {
        public override Task<ApiResult<PagedResult<RecipeSummaryDto>>> ListAsync(RecipeQuery query, int page, CancellationToken ct = default) =>
            Task.FromResult(error is null
                ? ApiResult<PagedResult<RecipeSummaryDto>>.Ok(new PagedResult<RecipeSummaryDto>([Country], page, 20, 1))
                : ApiResult<PagedResult<RecipeSummaryDto>>.Fail(error));




    }

    private sealed class StubState(RecipesState value) : IState<RecipesState>
    {
        public event EventHandler? StateChanged;

        public RecipesState Value => value;

        public void Raise() => StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingDispatcher : IDispatcher
    {
        public event EventHandler<ActionDispatchedEventArgs>? ActionDispatched;

        public List<object> Actions { get; } = [];

        public void Dispatch(object action)
        {
            Actions.Add(action);
            ActionDispatched?.Invoke(this, new ActionDispatchedEventArgs(action));
        }
    }
}
