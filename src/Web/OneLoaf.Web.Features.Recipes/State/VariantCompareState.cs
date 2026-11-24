using Fluxor;
using OneLoaf.Web.Features.Recipes.Services;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.Features.Recipes.State;

/// <summary>A comparison with the titles of both recipes for the header and the column names.</summary>
public sealed record VariantComparison(RecipeComparisonDto Comparison, string VariantTitle, string ParentTitle);

/// <summary>Variant comparison slice (US-08).</summary>
[FeatureState(Name = "VariantCompare")]
public sealed record VariantCompareState(Guid? RecipeId, Loadable<VariantComparison> Data)
{
    private VariantCompareState()
        : this(null, Loadable<VariantComparison>.Idle)
    {
    }
}

/// <summary>Intent: compare the variant with its parent.</summary>
public sealed record LoadComparison(Guid VariantId);

/// <summary>Result: comparison loaded or failed.</summary>
public sealed record ComparisonLoaded(Guid VariantId, Loadable<VariantComparison> Data);

/// <summary>Reducers of <see cref="VariantCompareState"/>.</summary>
public static class VariantCompareReducers
{
    /// <summary>Request starts.</summary>
    [ReducerMethod]
    public static VariantCompareState OnLoad(VariantCompareState state, LoadComparison action) =>
        new(action.VariantId, state.RecipeId == action.VariantId ? state.Data.Reload() : new Loadable<VariantComparison>.Loading(null));

    /// <summary>Result for the current variant.</summary>
    [ReducerMethod]
    public static VariantCompareState OnLoaded(VariantCompareState state, ComparisonLoaded action) =>
        action.VariantId == state.RecipeId ? state with { Data = action.Data } : state;

    /// <summary>Any recipe change makes the comparison stale.</summary>
    [ReducerMethod(typeof(RecipeSaved))]
    public static VariantCompareState OnRecipeSaved(VariantCompareState state) => new(null, Loadable<VariantComparison>.Idle);
}

/// <summary>Loads the comparison and both titles (the comparison itself carries ids only).</summary>
public sealed class VariantCompareEffects(IRecipesApi api)
{
    /// <summary>Comparison, then the two recipes for their titles.</summary>
    [EffectMethod]
    public async Task HandleLoad(LoadComparison action, IDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(dispatcher);
        var comparisonTask = api.CompareAsync(action.VariantId);
        var variantTask = api.GetAsync(action.VariantId);
        if (await comparisonTask is not ApiResult<RecipeComparisonDto>.Success { Value: var comparison })
        {
            var error = ((ApiResult<RecipeComparisonDto>.Failure)await comparisonTask).Error;
            dispatcher.Dispatch(new ComparisonLoaded(action.VariantId, new Loadable<VariantComparison>.Failed(error)));
            return;
        }

        var parent = await api.GetAsync(comparison.ParentRecipeId);
        var variant = await variantTask;
        dispatcher.Dispatch(new ComparisonLoaded(action.VariantId, new Loadable<VariantComparison>.Loaded(new VariantComparison(
            comparison,
            (variant as ApiResult<RecipeDto>.Success)?.Value.Title ?? "Variant",
            (parent as ApiResult<RecipeDto>.Success)?.Value.Title ?? "Original"))));
    }
}
