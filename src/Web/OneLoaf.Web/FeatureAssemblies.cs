using System.Reflection;
using OneLoaf.Web.Features.Assistant;
using OneLoaf.Web.Features.History;
using OneLoaf.Web.Features.Library;
using OneLoaf.Web.Features.Production;
using OneLoaf.Web.Features.Recipes;
using OneLoaf.Web.Shared.Auth;

namespace OneLoaf.Web;

/// <summary>Assemblies the router and Fluxor scan besides the host.</summary>
internal static class FeatureAssemblies
{
    /// <summary>Feature libraries with routable pages.</summary>
    public static IReadOnlyList<Assembly> Pages { get; } =
    [
        typeof(RecipesFeature).Assembly,
        typeof(ProductionFeature).Assembly,
        typeof(HistoryFeature).Assembly,
        typeof(AssistantFeature).Assembly,
        typeof(LibraryFeature).Assembly,
    ];

    /// <summary>Assemblies with Fluxor features, reducers and effects: the shared layer and the features.</summary>
    public static IReadOnlyList<Assembly> Shared { get; } = [typeof(SessionState).Assembly, .. Pages];
}
