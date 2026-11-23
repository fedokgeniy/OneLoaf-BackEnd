using System.Text.Json.Nodes;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.Docs;

/// <summary>
/// Contract tests of the published API: the OpenAPI document each service generates must equal the version-controlled
/// one in <c>docs/api/reference</c>. A change of a route, a DTO or a status code fails here until the documentation is
/// regenerated (and reviewed in the pull request), so the docs and the code cannot drift apart.
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class OpenApiContractTests(ApiFixture fixture)
{
    private const string Regenerate =
        "Regenerate the docs: ONELOAF_EXPORT_DOCS=1 dotnet test tests/OneLoaf.IntegrationTests --filter FullyQualifiedName~ApiDocsGenerator";

    [Theory]
    [InlineData("core-api")]
    [InlineData("assistant")]
    [InlineData("notification")]
    public async Task GeneratedDocument_MatchesTheCommittedSpecification(string service)
    {
        using var client = service switch
        {
            "core-api" => fixture.Api.CreateClient(),
            "assistant" => fixture.CreateAssistant(new ScriptedClaude()).CreateClient(),
            _ => fixture.Notification.CreateClient(),
        };
        var generated = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));
        var path = Path.Combine(DocsPaths.Root, "docs", "api", "reference", $"{service}.openapi.json");
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));

        var differences = Differences(committed, generated, "$").Take(10).ToList();

        Assert.True(differences.Count == 0, $"{service}.openapi.json is out of date. {Regenerate}{Environment.NewLine}{string.Join(Environment.NewLine, differences)}");
    }

    private static IEnumerable<string> Differences(JsonNode? expected, JsonNode? actual, string path)
    {
        switch (expected, actual)
        {
            case (JsonObject e, JsonObject a):
                foreach (var key in e.Select(p => p.Key).Union(a.Select(p => p.Key), StringComparer.Ordinal))
                {
                    if (!e.ContainsKey(key))
                    {
                        yield return $"added   {path}.{key}";
                    }
                    else if (!a.ContainsKey(key))
                    {
                        yield return $"removed {path}.{key}";
                    }
                    else
                    {
                        foreach (var difference in Differences(e[key], a[key], $"{path}.{key}"))
                        {
                            yield return difference;
                        }
                    }
                }

                break;
            case (JsonArray e, JsonArray a) when e.Count == a.Count:
                for (var i = 0; i < e.Count; i++)
                {
                    foreach (var difference in Differences(e[i], a[i], $"{path}[{i}]"))
                    {
                        yield return difference;
                    }
                }

                break;
            default:
                if (!JsonNode.DeepEquals(expected, actual))
                {
                    yield return $"changed {path}: {expected?.ToJsonString()} -> {actual?.ToJsonString()}";
                }

                break;
        }
    }
}
