using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>
/// Answers API calls of the page with the real exchanges recorded in <c>docs/api/examples</c> (ADR-F09), so the
/// journeys need neither a database nor the services.
/// </summary>
public static class RecordedApi
{
    private static readonly string ExamplesRoot = Path.Combine(FindRepositoryRoot(), "docs", "api", "examples");

    /// <summary>Response body of a recorded case, e.g. <c>("core-api", "Login", "typical")</c>.</summary>
    public static (int Status, JsonNode? Body) Case(string service, string operationId, string caseName)
    {
        var path = Path.Combine(ExamplesRoot, service, operationId + ".json");
        var cases = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        var match = cases.FirstOrDefault(c => (string?)c!["case"] == caseName)
            ?? throw new InvalidOperationException($"No case '{caseName}' in {path}.");
        return ((int)match["status"]!, match["responseBody"]?.DeepClone());
    }

    /// <summary>Routes <paramref name="urlPattern"/> to a fixed status and body.</summary>
    public static Task RouteAsync(IPage page, string urlPattern, int status, JsonNode? body, string? contentType = null) =>
        page.RouteAsync(urlPattern, route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = status,
            ContentType = contentType ?? (status >= 400 ? "application/problem+json" : "application/json"),
            Body = body?.ToJsonString() ?? string.Empty,
        }));

    /// <summary>
    /// Recorded tokens are redacted (<c>&lt;payload&gt;</c>) and expire; the client only reads the claims, so the test
    /// puts in an unsigned token with the recorded user's claims and an expiry one hour ahead.
    /// </summary>
    public static JsonNode WithReadableToken(JsonNode loginResponse)
    {
        var user = loginResponse["user"]!;
        var claims = new JsonObject
        {
            ["sub"] = (string?)user["id"],
            ["email"] = (string?)user["email"],
            ["name"] = (string?)user["displayName"],
            ["role"] = (string?)user["role"],
        };
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(claims.ToJsonString()))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        loginResponse["accessToken"] = $"e30.{payload}.signature";
        loginResponse["expiresAt"] = DateTimeOffset.UtcNow.AddHours(1).ToString("O");
        return loginResponse;
    }

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "OneLoaf.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root with OneLoaf.slnx not found.");
    }
}
