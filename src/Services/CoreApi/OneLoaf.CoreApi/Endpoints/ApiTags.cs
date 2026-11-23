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
}
