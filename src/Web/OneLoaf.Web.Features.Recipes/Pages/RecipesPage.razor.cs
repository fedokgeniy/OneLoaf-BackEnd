using System.Globalization;
using Fluxor;
using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Features.Recipes.State;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.Features.Recipes.Pages;

/// <summary>
/// <c>/recipes</c> (US-05, US-07, US-37): search, filters and the recipe cards. The filter lives in the Fluxor slice
/// (it survives a visit to a recipe) and in the query string (<c>?search=rye&amp;archived=true&amp;minProtein=12</c>).
/// </summary>
public sealed partial class RecipesPage
{
    private static readonly IReadOnlyList<SegmentOption<decimal?>> ProteinOptions =
    [
        new(null, "Any flour"),
        new(11m, "≥ 11 %"),
        new(12m, "≥ 12 %"),
        new(13m, "≥ 13 %"),
    ];

    private string _search = string.Empty;

    [Inject]
    private IState<RecipesState> State { get; set; } = default!;

    [Inject]
    private IDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>Title search from the query string.</summary>
    [SupplyParameterFromQuery(Name = "search")]
    public string? SearchParameter { get; set; }

    /// <summary>Archived flag from the query string.</summary>
    [SupplyParameterFromQuery(Name = "archived")]
    public bool? ArchivedParameter { get; set; }

    /// <summary>Minimum flour protein from the query string.</summary>
    [SupplyParameterFromQuery(Name = "minProtein")]
    public string? MinProteinParameter { get; set; }

    private IReadOnlyList<RecipeCardModel> Cards => RecipesSelectors.VisibleCards(State.Value);

    private bool IsLoading => State.Value.List is Loadable<RecipeList>.Loading;

    private bool IsFiltered => State.Value.Query.IsFiltered || State.Value.VariantsOnly;

    private string Announcement => State.Value.List switch
    {
        Loadable<RecipeList>.Loading => "Loading recipes",
        Loadable<RecipeList>.Loaded { Data: var list } => list.TotalCount == 1 ? "1 recipe" : $"{list.TotalCount} recipes",
        _ => string.Empty,
    };

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        var fromUrl = QueryFromUrl();
        _search = fromUrl.Search;
        if (fromUrl != State.Value.Query || State.Value.List is Loadable<RecipeList>.NotAsked)
        {
            Dispatcher.Dispatch(new SetRecipeQuery(fromUrl, Debounce: false));
        }
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        _search = e.Value as string ?? string.Empty;
        Apply(State.Value.Query with { Search = _search.Trim() }, debounce: true);
    }

    private void OnArchivedChanged(bool includeArchived) =>
        Apply(State.Value.Query with { IncludeArchived = includeArchived }, debounce: false);

    private void OnProteinChanged(decimal? minProtein) =>
        Apply(State.Value.Query with { MinProteinPercent = minProtein }, debounce: false);

    private void ClearFilters()
    {
        _search = string.Empty;
        Dispatcher.Dispatch(new SetVariantsOnly(false));
        Apply(RecipeQuery.None, debounce: false);
    }

    private void Retry() => Dispatcher.Dispatch(new LoadRecipes(State.Value.Query));

    /// <summary>Dispatches the filter and mirrors it in the address bar (replace, so Back leaves the page).</summary>
    private void Apply(RecipeQuery query, bool debounce)
    {
        Dispatcher.Dispatch(new SetRecipeQuery(query, debounce));
        var uri = Navigation.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["search"] = query.Search.Length > 0 ? query.Search : null,
            ["archived"] = query.IncludeArchived ? "true" : null,
            ["minProtein"] = query.MinProteinPercent?.ToString(CultureInfo.InvariantCulture),
        });
        Navigation.NavigateTo(uri, replace: true);
    }

    private RecipeQuery QueryFromUrl() => new(
        (SearchParameter ?? string.Empty).Trim(),
        ArchivedParameter == true,
        decimal.TryParse(MinProteinParameter, NumberStyles.Number, CultureInfo.InvariantCulture, out var protein)
            ? protein
            : null);
}
