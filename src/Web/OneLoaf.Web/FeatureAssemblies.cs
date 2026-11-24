using System.Reflection;
using System.Text.RegularExpressions;
using OneLoaf.Web.Features.Production;
using OneLoaf.Web.Features.Recipes;
using OneLoaf.Web.Shared.Auth;

namespace OneLoaf.Web;

/// <summary>Assemblies the router and Fluxor scan besides the host, and the lazy-loaded features (ADR-F04).</summary>
internal static partial class FeatureAssemblies
{
    /// <summary>Feature libraries loaded at startup (used in every session).</summary>
    public static IReadOnlyList<Assembly> Pages { get; } =
    [
        typeof(RecipesFeature).Assembly,
        typeof(ProductionFeature).Assembly,
    ];

    /// <summary>Assemblies with Fluxor features, reducers and effects scanned at startup: the shared layer and the eager features.</summary>
    public static IReadOnlyList<Assembly> Shared { get; } = [typeof(SessionState).Assembly, .. Pages];

    /// <summary>Lazy features: the routes that need them and their file (<c>BlazorWebAssemblyLazyLoad</c> in the csproj).</summary>
    public static IReadOnlyList<(Regex Route, string File)> Lazy { get; } =
    [
        (HistoryRoute(), "OneLoaf.Web.Features.History.wasm"),
        (AssistantRoute(), "OneLoaf.Web.Features.Assistant.wasm"),
        (LibraryRoute(), "OneLoaf.Web.Features.Library.wasm"),
    ];

    /// <summary>Files to load for a base-relative path (without query).</summary>
    public static IReadOnlyList<string> FilesFor(string path) => [.. Lazy.Where(l => l.Route.IsMatch(path)).Select(l => l.File)];

    [GeneratedRegex("^history(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex HistoryRoute();

    [GeneratedRegex("^assistant(/|$)", RegexOptions.IgnoreCase)]
    private static partial Regex AssistantRoute();

    [GeneratedRegex("^(library|admin)(/|$)|^recipes/[^/]+/publish$", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryRoute();
}
