using OneLoaf.Web.Shared.Ui.Icons;

namespace OneLoaf.Web.Layout;

/// <summary>A destination of the main navigation.</summary>
/// <param name="Href">Base-relative route (empty string is Home).</param>
/// <param name="Text">Label.</param>
/// <param name="Icon">Icon.</param>
public sealed record NavDestination(string Href, string Text, IconName Icon);

/// <summary>Destinations per navigation pattern (screens-and-routes.md § Navigation).</summary>
public static class NavDestinations
{
    /// <summary>Home.</summary>
    public static readonly NavDestination Home = new(string.Empty, "Home", IconName.Home);

    /// <summary>Recipes.</summary>
    public static readonly NavDestination Recipes = new("recipes", "Recipes", IconName.Recipes);

    /// <summary>Batches (calendar).</summary>
    public static readonly NavDestination Batches = new("batches", "Batches", IconName.Batches);

    /// <summary>Starters.</summary>
    public static readonly NavDestination Starters = new("starters", "Starters", IconName.Starter);

    /// <summary>Assistant.</summary>
    public static readonly NavDestination Assistant = new("assistant", "Assistant", IconName.Assistant);

    /// <summary>Library.</summary>
    public static readonly NavDestination Library = new("library", "Library", IconName.Library);

    /// <summary>History.</summary>
    public static readonly NavDestination History = new("history", "History", IconName.History);

    /// <summary>Ingredients.</summary>
    public static readonly NavDestination Ingredients = new("ingredients", "Ingredients", IconName.Calculator);

    /// <summary>Kitchen settings: oven, mixer, tariffs and the other figures the calculator uses.</summary>
    public static readonly NavDestination Kitchen = new("settings/kitchen", "My kitchen", IconName.Kitchen);

    /// <summary>Settings.</summary>
    public static readonly NavDestination Settings = new("settings", "Settings", IconName.Settings);

    /// <summary>Phone bottom bar: five items.</summary>
    public static IReadOnlyList<NavDestination> BottomBar { get; } = [Home, Recipes, Batches, Starters, Assistant];

    /// <summary>Tablet rail and desktop sidebar: the seven main items at the top.</summary>
    public static IReadOnlyList<NavDestination> Main { get; } = [Home, Recipes, Batches, Starters, Assistant, Library, History];

    /// <summary>Tablet rail and desktop sidebar: the workspace set-up pinned to the bottom.</summary>
    public static IReadOnlyList<NavDestination> Pinned { get; } = [Ingredients, Kitchen];

    /// <summary>Phone avatar menu: what does not fit in the bottom bar.</summary>
    public static IReadOnlyList<NavDestination> AccountMenu { get; } = [Library, History, Ingredients, Kitchen, Settings];
}
