using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Contracts;

/// <summary>
/// Every 2xx body recorded in docs/api/examples for an operation the client calls must deserialize into its record
/// with unmapped members disallowed: a renamed or new API field fails here (ADR-F06).
/// </summary>
public sealed class RecordedExamplesTests
{
    /// <summary>Operation → response record. Add a row for every operation a typed API service calls.</summary>
    private static readonly Dictionary<string, Type> Mapping = new()
    {
        ["core-api/Register"] = typeof(RegisterResponse),
        ["core-api/Login"] = typeof(LoginResponse),
        ["core-api/GetMe"] = typeof(UserDto),
        ["core-api/ListRecipes"] = typeof(PagedResult<RecipeSummaryDto>),
    };

    private static readonly JsonSerializerOptions Strict = new(new ApiJsonOptions([SharedJsonContext.Default]).Options)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static TheoryData<string> Operations() => [.. Mapping.Keys];

    [Theory]
    [MemberData(nameof(Operations))]
    public void ResponseBody_EverySuccessCase_DeserializesWithoutUnknownFields(string operation)
    {
        var path = Path.Combine(RepositoryRoot(), "docs", "api", "examples", operation + ".json");
        var cases = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        var bodies = cases.Where(c => (int)c!["status"]! is >= 200 and < 300 && c["responseBody"] is not null).ToList();

        Assert.NotEmpty(bodies);
        foreach (var body in bodies)
        {
            var value = body!["responseBody"]!.Deserialize(Mapping[operation], Strict);
            Assert.NotNull(value);
        }
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OneLoaf.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
