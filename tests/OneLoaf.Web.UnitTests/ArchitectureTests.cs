using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using OneLoaf.Web.Features.Assistant;
using OneLoaf.Web.Features.History;
using OneLoaf.Web.Features.Library;
using OneLoaf.Web.Features.Production;
using OneLoaf.Web.Features.Recipes;
using OneLoaf.Web.Shared.Api;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace OneLoaf.Web.UnitTests;

/// <summary>Feature-sliced dependency rules of the web client (architecture.md §2, ADR-F03).</summary>
public sealed class ArchitectureTests
{
    private static readonly Assembly SharedAssembly = typeof(ApiClient).Assembly;

    private static readonly Dictionary<string, Assembly> Features = new(StringComparer.Ordinal)
    {
        ["Recipes"] = typeof(RecipesFeature).Assembly,
        ["Production"] = typeof(ProductionFeature).Assembly,
        ["History"] = typeof(HistoryFeature).Assembly,
        ["Assistant"] = typeof(AssistantFeature).Assembly,
        ["Library"] = typeof(LibraryFeature).Assembly,
    };

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies([SharedAssembly, typeof(App).Assembly, .. Features.Values])
        .Build();

    public static TheoryData<string> FeatureNames => [.. Features.Keys];

    [Fact]
    public void Shared_DoesNotDependOnFeaturesOrHost() =>
        Types().That().ResideInAssembly(SharedAssembly)
            .Should().NotDependOnAny(Types(true).That().ResideInNamespaceMatching(@"^OneLoaf\.Web\.(Features|Layout|Pages|Account)(\..+)?$"))
            .Check(Architecture);

    [Theory]
    [MemberData(nameof(FeatureNames))]
    public void Feature_DoesNotDependOnOtherFeaturesOrHost(string feature) =>
        Types().That().ResideInAssembly(Features[feature])
            .Should().NotDependOnAny(Types(true).That().ResideInNamespaceMatching(
                $@"^OneLoaf\.Web\.(Features\.(?!{feature}(\.|$))[A-Za-z]+|Layout|Pages|Account)(\..+)?$"))
            .WithoutRequiringPositiveResults()
            .Check(Architecture);

    [Fact]
    public void UiComponents_DoNotUseApiOrState() =>
        Types().That().ResideInNamespace("OneLoaf.Web.Shared.Ui")
            .Should().NotDependOnAny(Types(true).That().ResideInNamespaceMatching(@"^(OneLoaf\.Web\.Shared\.Api|Fluxor)(\..+)?$"))
            .Check(Architecture);
}
