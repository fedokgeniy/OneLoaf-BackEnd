namespace OneLoaf.CoreApi.Endpoints;

/// <summary>OpenAPI tags (one per area).</summary>
public static class ApiTags
{
    /// <summary>Registration, login, logout.</summary>
    public const string Auth = "Auth";

    /// <summary>The current user's account.</summary>
    public const string Account = "Account";

    /// <summary>Reference data.</summary>
    public const string Reference = "Reference";

    /// <summary>Kitchen settings and warnings.</summary>
    public const string Kitchen = "Kitchen";

    /// <summary>Flours, ingredients and prices.</summary>
    public const string Catalog = "Catalog";

    /// <summary>Recipes and variants.</summary>
    public const string Recipes = "Recipes";

    /// <summary>Calculations of a recipe.</summary>
    public const string Calculator = "Calculator";

    /// <summary>Batches and stages.</summary>
    public const string Production = "Production";

    /// <summary>Starters and feedings.</summary>
    public const string Starters = "Starters";

    /// <summary>Baking history.</summary>
    public const string History = "History";

    /// <summary>Shared recipe library.</summary>
    public const string Library = "Library";

    /// <summary>Moderation of the library.</summary>
    public const string Moderation = "Moderation";
}
