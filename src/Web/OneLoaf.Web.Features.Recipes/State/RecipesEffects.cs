using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State.Notifications;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>Side effects of the recipes slice: debounced search, first page, next pages.</summary>
public sealed class RecipesEffects(IRecipesApi api, IState<RecipesState> state, TimeProvider time) : IDisposable
{
    /// <summary>Pause after the last keystroke before the list is requested.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(300);

    private CancellationTokenSource? _pending;

    /// <summary>Waits for the user to stop typing, then loads; a newer filter cancels the older one.</summary>
    [EffectMethod]
    public async Task HandleSetQuery(SetRecipeQuery action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var cts = Restart();
        if (action.Debounce)
        {
            try
            {
                await Task.Delay(SearchDelay, time, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }

        dispatcher.Dispatch(new LoadRecipes(action.Query));
    }

    /// <summary>Loads the first page.</summary>
    [EffectMethod]
    public async Task HandleLoad(LoadRecipes action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var result = await api.ListAsync(action.Query, 1);
        dispatcher.Dispatch(result switch
        {
            ApiResult<PagedResult<RecipeSummaryDto>>.Success { Value: var page } => new RecipesLoaded(action.Query, ToList(page)),
            ApiResult<PagedResult<RecipeSummaryDto>>.Failure { Error: var error } => new RecipesLoadFailed(action.Query, error),
            _ => throw new InvalidOperationException("Unknown result."),
        });
    }

    /// <summary>Loads the next page of the current filter.</summary>
    [EffectMethod(typeof(LoadMoreRecipes))]
    public async Task HandleLoadMore(IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        if (!state.Value.LoadingMore || state.Value.List.Value is not { } list)
        {
            return;
        }

        var query = state.Value.Query;
        var result = await api.ListAsync(query, list.Page + 1);
        switch (result)
        {
            case ApiResult<PagedResult<RecipeSummaryDto>>.Success { Value: var page }:
                dispatcher.Dispatch(new MoreRecipesLoaded(query, ToList(page)));
                break;
            case ApiResult<PagedResult<RecipeSummaryDto>>.Failure { Error: var error }:
                dispatcher.Dispatch(new MoreRecipesLoadFailed(error));
                dispatcher.Dispatch(new ShowToast(Toast.Error(error)));
                break;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _pending?.Dispose();

    private static RecipeList ToList(PagedResult<RecipeSummaryDto> page) =>
        new([.. page.Items], page.Page, page.TotalCount, page.HasMore);

    private CancellationTokenSource Restart()
    {
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        return _pending;
    }
}
