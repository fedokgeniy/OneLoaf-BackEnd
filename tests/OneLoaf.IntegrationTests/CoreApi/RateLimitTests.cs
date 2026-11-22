using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.IntegrationTests.Infrastructure;
using OneLoaf.ServiceDefaults.Errors;

namespace OneLoaf.IntegrationTests.CoreApi;

[Collection(ApiTestGroup.Name)]
public sealed class RateLimitTests(ApiFixture fixture)
{
    private const int PermitLimit = 3;

    [Fact]
    public async Task US02_LoginAttemptsAboveLimit_Return429WithRetryAfter()
    {
        var api = fixture.CreateApi(PermitLimit);
        using var client = api.CreateClient();
        var request = new LoginRequest($"nobody-{Guid.NewGuid():N}@example.com", "wrong-password");

        for (var i = 0; i < PermitLimit; i++)
        {
            using var allowed = await client.PostAsJsonAsync("/api/v1/auth/login", request, CoreApiFactory.Json, Ct);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync("/api/v1/auth/login", request, CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.NotNull(rejected.Headers.RetryAfter);
        using var problem = JsonDocument.Parse(await rejected.Content.ReadAsStringAsync(Ct));
        Assert.Equal(ProblemTypes.TooManyRequests, problem.RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task OtherEndpoints_AreNotLimitedByTheAuthPolicy()
    {
        var api = fixture.CreateApi(PermitLimit);
        using var client = api.CreateClient();

        for (var i = 0; i < PermitLimit + 2; i++)
        {
            using var response = await client.GetAsync("/health/live", Ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
