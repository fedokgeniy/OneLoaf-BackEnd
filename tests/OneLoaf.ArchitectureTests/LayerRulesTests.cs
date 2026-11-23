using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using OneLoaf.CoreApi.Application;
using OneLoaf.CoreApi.Infrastructure;
using OneLoaf.Domain.Entities;
using OneLoaf.ServiceDefaults;
using OneLoaf.SharedKernel.Security;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace OneLoaf.ArchitectureTests;

/// <summary>Layer dependency rules of the solution.</summary>
public sealed class LayerRulesTests
{
    private static readonly Assembly DomainAssembly = typeof(User).Assembly;
    private static readonly Assembly SharedKernelAssembly = typeof(ICurrentUser).Assembly;
    private static readonly Assembly ServiceDefaultsAssembly = typeof(Extensions).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(OneLoaf.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly CoreApiAssembly = typeof(Program).Assembly;
    private static readonly Assembly CoreApiApplicationAssembly = typeof(CoreApiApplicationAssembly).Assembly;
    private static readonly Assembly CoreApiInfrastructureAssembly = typeof(CoreApi.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly AssistantAssembly = typeof(Assistant.Endpoints.AssistantEntryPoint).Assembly;
    private static readonly Assembly AssistantApplicationAssembly = typeof(Assistant.Application.DependencyInjection).Assembly;
    private static readonly Assembly AssistantInfrastructureAssembly = typeof(Assistant.Infrastructure.DependencyInjection).Assembly;

    /// <summary>Application assemblies of all services.</summary>
    private static readonly Assembly[] ApplicationAssemblies = [CoreApiApplicationAssembly, AssistantApplicationAssembly];

    /// <summary>Presentation (web) assemblies of all services.</summary>
    private static readonly Assembly[] PresentationAssemblies = [CoreApiAssembly, AssistantAssembly];

    /// <summary>Assemblies of each service by service prefix.</summary>
    private static readonly Dictionary<string, Assembly[]> ServiceAssemblies = new(StringComparer.Ordinal)
    {
        ["OneLoaf.CoreApi"] = [CoreApiAssembly, CoreApiApplicationAssembly, CoreApiInfrastructureAssembly],
        ["OneLoaf.Assistant"] = [AssistantAssembly, AssistantApplicationAssembly, AssistantInfrastructureAssembly],
    };

    /// <summary>Prefixes of every service, also those not created yet, so a future cross-reference fails.</summary>
    private static readonly string[] ServicePrefixes = ["OneLoaf.CoreApi", "OneLoaf.Assistant", "OneLoaf.Notification"];

    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(DomainAssembly, SharedKernelAssembly, ServiceDefaultsAssembly, InfrastructureAssembly,
            CoreApiAssembly, CoreApiApplicationAssembly, CoreApiInfrastructureAssembly,
            AssistantAssembly, AssistantApplicationAssembly, AssistantInfrastructureAssembly)
        .Build();

    /// <summary>Namespaces the Application layer must never use.</summary>
    public static TheoryData<string> ForbiddenInApplication => new()
    {
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "RabbitMQ",
        "Npgsql",
        "StackExchange.Redis",
        "Anthropic",
        "OneLoaf.Infrastructure",
        "OneLoaf.ServiceDefaults",
        "OneLoaf.Messaging.RabbitMq",
    };

    [Theory]
    [MemberData(nameof(ForbiddenInApplication))]
    public void Application_DoesNotDependOn(string forbiddenNamespace)
    {
        foreach (var assembly in ApplicationAssemblies)
        {
            Types().That().ResideInAssembly(assembly)
                .Should().NotDependOnAny(Types(true).That().ResideInNamespaceMatching(NamespaceOrChild(forbiddenNamespace)))
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }
    }

    [Fact]
    public void Application_DoesNotReferenceInfrastructureAssemblies()
    {
        foreach (var assembly in ApplicationAssemblies)
        {
            var references = ReferencedOneLoafAssemblies(assembly);
            Assert.DoesNotContain(references, name => name.EndsWith(".Infrastructure", StringComparison.Ordinal));
            Assert.DoesNotContain(references, name => name == ServiceDefaultsAssembly.GetName().Name);
        }
    }

    [Fact]
    public void Domain_DependsOnNothing()
    {
        Types().That().ResideInAssembly(DomainAssembly)
            .Should().NotDependOnAny(Types(true).That().DoNotResideInNamespaceMatching(@"^(System|OneLoaf\.Domain|Microsoft\.CodeAnalysis)(\..+)?$"))
            .Check(Architecture);
        Assert.Empty(ReferencedOneLoafAssemblies(DomainAssembly));
    }

    [Fact]
    public void SharedKernel_DependsOnNothing()
    {
        Types().That().ResideInAssembly(SharedKernelAssembly)
            .Should().NotDependOnAny(Types(true).That().DoNotResideInNamespaceMatching(@"^(System|OneLoaf\.SharedKernel|Microsoft\.CodeAnalysis)(\..+)?$"))
            .Check(Architecture);
        Assert.Empty(ReferencedOneLoafAssemblies(SharedKernelAssembly));
    }

    [Fact]
    public void Presentation_UsesInfrastructureOnlyInProgram()
    {
        foreach (var assembly in PresentationAssemblies)
        {
            Types().That().ResideInAssembly(assembly)
                .And().DoNotHaveFullNameMatching(@"^Program($|\+|<)")
                .And().DoNotHaveFullNameMatching(@"^<Program>")
                .Should().NotDependOnAny(Types(true).That().ResideInNamespaceMatching(@"^OneLoaf(\.CoreApi|\.Assistant|\.Notification)?\.Infrastructure(\..+)?$"))
                .WithoutRequiringPositiveResults()
                .Check(Architecture);
        }
    }

    [Fact]
    public void Services_DoNotReferenceEachOther()
    {
        foreach (var (prefix, assemblies) in ServiceAssemblies)
        {
            var otherServices = ServicePrefixes.Where(p => p != prefix).ToArray();
            foreach (var assembly in assemblies)
            {
                var references = ReferencedOneLoafAssemblies(assembly);
                Assert.DoesNotContain(references, name => otherServices.Any(other =>
                    name == other || name.StartsWith(other + ".", StringComparison.Ordinal)));
            }
        }
    }

    [Fact]
    public void SharedProjects_DoNotReferenceServices()
    {
        Assembly[] shared = [DomainAssembly, SharedKernelAssembly, ServiceDefaultsAssembly, InfrastructureAssembly];
        foreach (var assembly in shared)
        {
            var references = ReferencedOneLoafAssemblies(assembly);
            Assert.DoesNotContain(references, name => ServicePrefixes.Any(p =>
                name == p || name.StartsWith(p + ".", StringComparison.Ordinal)));
        }
    }

    private static string NamespaceOrChild(string ns) => $"^{ns.Replace(".", @"\.", StringComparison.Ordinal)}(\\..+)?$";

    private static string[] ReferencedOneLoafAssemblies(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .Where(name => name.StartsWith("OneLoaf.", StringComparison.Ordinal))
            .ToArray();
}
