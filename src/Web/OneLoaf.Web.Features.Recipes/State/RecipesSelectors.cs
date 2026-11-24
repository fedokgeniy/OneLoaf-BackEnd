using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>A recipe card: the summary and, for a variant, the parent's title when it is among the loaded recipes.</summary>
public sealed record RecipeCardModel(RecipeSummaryDto Recipe, string? ParentTitle)
{
    /// <summary>The recipe is a variant of another one (US-07).</summary>
    public bool IsVariant => Recipe.ParentRecipeId is not null;
}

/// <summary>Derived views of <see cref="RecipesState"/>.</summary>
public static class RecipesSelectors
{
    private static (RecipeList? List, bool VariantsOnly, IReadOnlyList<RecipeCardModel> Cards) _last;

    /// <summary>
    /// Cards to show, memoized by input reference: re-renders that do not change the list or the toggle reuse the
    /// previous result instead of rebuilding the parent lookup.
    /// </summary>
    public static IReadOnlyList<RecipeCardModel> VisibleCards(RecipesState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var list = state.List.Value;
        var last = _last;
        if (ReferenceEquals(last.List, list) && last.VariantsOnly == state.VariantsOnly && last.Cards is not null)
        {
            return last.Cards;
        }

        IReadOnlyList<RecipeCardModel> cards = [];
        if (list is not null)
        {
            var titles = list.Items.ToDictionary(r => r.Id, r => r.Title);
            cards = list.Items
                .Where(r => !state.VariantsOnly || r.ParentRecipeId is not null)
                .Select(r => new RecipeCardModel(
                    r, r.ParentRecipeId is { } parent ? titles.GetValueOrDefault(parent) : null))
                .ToList();
        }

        _last = (list, state.VariantsOnly, cards);
        return cards;
    }
}
