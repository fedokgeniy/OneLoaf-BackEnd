namespace OneLoaf.CoreApi.Application.Calculations;

/// <summary>
/// The calculation functions of the database: the only implementation of cost, timeline, windows and production
/// figures. They do not check ownership; callers do.
/// </summary>
public interface IRecipeCalculations
{
    /// <summary><c>fn_recipe_cost(recipe, loaves, asOf)</c>.</summary>
    Task<IReadOnlyList<CostLine>> GetCostAsync(Guid recipeId, int loaves, DateOnly asOf, CancellationToken cancellationToken);

    /// <summary><c>fn_recipe_timeline(recipe, start)</c>.</summary>
    Task<IReadOnlyList<TimelineStageDto>> GetTimelineAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken);

    /// <summary><c>fn_recipe_ready_window(recipe, start)</c>.</summary>
    Task<TimeWindow> GetReadyWindowAsync(Guid recipeId, DateTimeOffset start, CancellationToken cancellationToken);

    /// <summary><c>fn_recipe_start_window(recipe, readyAt)</c>.</summary>
    Task<TimeWindow> GetStartWindowAsync(Guid recipeId, DateTimeOffset readyAt, CancellationToken cancellationToken);

    /// <summary>Dough weight, kneading cycles and starter amount for a number of loaves.</summary>
    Task<ProductionFigures> GetProductionFiguresAsync(Guid recipeId, int loaves, CancellationToken cancellationToken);
}
