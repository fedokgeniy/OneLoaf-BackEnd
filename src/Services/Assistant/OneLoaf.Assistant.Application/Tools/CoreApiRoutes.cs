namespace OneLoaf.Assistant.Application.Tools;

/// <summary>Core API routes used by the tools.</summary>
public static class CoreApiRoutes
{
    /// <summary>Base path.</summary>
    public const string Base = "/api/v1";

    /// <summary>Recipes.</summary>
    public const string Recipes = Base + "/recipes";

    /// <summary>Recipe with ingredients given by name.</summary>
    public const string RecipeDrafts = Recipes + "/drafts";

    /// <summary>Starters.</summary>
    public const string Starters = Base + "/starters";

    /// <summary>Batches.</summary>
    public const string Batches = Base + "/batches";

    /// <summary>Plan preview.</summary>
    public const string BatchPreview = Batches + "/preview";

    /// <summary>Baking history.</summary>
    public const string BakeRecords = Base + "/bake-records";

    /// <summary>Ingredients.</summary>
    public const string Ingredients = Base + "/ingredients";

    /// <summary>Data quality warnings.</summary>
    public const string Warnings = Base + "/warnings";

    /// <summary>Kitchen settings (time zone).</summary>
    public const string KitchenSettings = Base + "/kitchen-settings";
}
