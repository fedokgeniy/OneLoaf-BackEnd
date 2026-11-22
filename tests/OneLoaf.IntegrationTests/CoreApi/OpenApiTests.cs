using System.Text.Json;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

[Collection(ApiTestGroup.Name)]
public sealed class OpenApiTests(ApiFixture fixture)
{
    [Theory]
    [InlineData("/api/v1/auth/register", "post", false)]
    [InlineData("/api/v1/auth/login", "post", false)]
    [InlineData("/api/v1/auth/logout", "post", true)]
    [InlineData("/api/v1/me", "get", true)]
    [InlineData("/api/v1/me", "delete", true)]
    public async Task Document_DescribesEndpointWithSummaryAndSecurity(string path, string method, bool requiresToken)
    {
        using var client = fixture.Api.CreateClient();
        using var document = JsonDocument.Parse(
            await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));

        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

        Assert.False(string.IsNullOrWhiteSpace(operation.GetProperty("summary").GetString()));
        Assert.Equal(requiresToken, operation.TryGetProperty("security", out _));
    }
}
