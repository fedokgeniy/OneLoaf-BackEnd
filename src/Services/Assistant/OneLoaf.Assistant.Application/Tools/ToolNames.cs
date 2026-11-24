namespace OneLoaf.Assistant.Application.Tools;

/// <summary>Names of the assistant tools (the model sees these names).</summary>
public static class ToolNames
{
    /// <summary>Find recipes by title or flour protein.</summary>
    public const string FindRecipes = "find_recipes";

    /// <summary>Recipe card.</summary>
    public const string GetRecipe = "get_recipe";

    /// <summary>Cost breakdown.</summary>
    public const string CalculateCost = "calculate_cost";

    /// <summary>Timeline and ready window.</summary>
    public const string CalculateTimeline = "calculate_timeline";

    /// <summary>Start window for a ready time.</summary>
    public const string CalculateStartWindow = "calculate_start_window";

    /// <summary>Dough weight, kneading cycles, starter amount.</summary>
    public const string CalculateProductionFigures = "calculate_production_figures";

    /// <summary>Starters with their schedule.</summary>
    public const string ListStarters = "list_starters";

    /// <summary>Plan of a batch without saving.</summary>
    public const string PreviewBatchPlan = "preview_batch_plan";

    /// <summary>Plan of a batch saved as a pending action for the baker to confirm.</summary>
    public const string ProposeBatch = "propose_batch";

    /// <summary>A new recipe saved as a pending action for the baker to confirm.</summary>
    public const string ProposeRecipe = "propose_recipe";

    /// <summary>Stage log and feedback of a bake.</summary>
    public const string GetBakeAnalysisData = "get_bake_analysis_data";

    /// <summary>Price history of an ingredient.</summary>
    public const string GetIngredientPrices = "get_ingredient_prices";

    /// <summary>Missing data warnings.</summary>
    public const string GetMissingData = "get_missing_data";
}
