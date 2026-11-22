using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

[Collection(ApiTestGroup.Name)]
public sealed class AccountTests(ApiFixture fixture)
{
    private const string Me = "/api/v1/me";

    [Fact]
    public async Task Me_WithoutToken_Returns401ProblemWithCorrelationId()
    {
        using var client = fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "it-401");

        using var response = await client.GetAsync(Me, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        Assert.Equal("it-401", problem.RootElement.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task Me_WithForgedToken_Returns401()
    {
        using var client = fixture.Api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJub25lIn0.e30.");

        using var response = await client.GetAsync(Me, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task US03_EachUserSeesOnlyOwnProfile()
    {
        var anna = await fixture.Api.RegisterAsync("Anna");
        var boris = await fixture.Api.RegisterAsync("Boris");
        using var annaClient = await fixture.Api.CreateAuthenticatedClientAsync(anna);
        using var borisClient = await fixture.Api.CreateAuthenticatedClientAsync(boris);

        var annaProfile = await annaClient.GetFromJsonAsync<UserProfileDto>(Me, CoreApiFactory.Json, Ct);
        var borisProfile = await borisClient.GetFromJsonAsync<UserProfileDto>(Me, CoreApiFactory.Json, Ct);

        Assert.Equal(anna.Id, annaProfile!.Id);
        Assert.Equal(boris.Id, borisProfile!.Id);
        Assert.DoesNotContain(anna.Email, JsonSerializer.Serialize(borisProfile, CoreApiFactory.Json), StringComparison.Ordinal);
    }

    [Fact]
    public async Task US04_DeleteWithPassword_RemovesAccountAndWorkspaceOnly()
    {
        var anna = await fixture.Api.RegisterAsync("Anna");
        var boris = await fixture.Api.RegisterAsync("Boris");
        using var annaClient = await fixture.Api.CreateAuthenticatedClientAsync(anna);

        using var delete = await SendDeleteAsync(annaClient, anna.Password);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        using var meAfter = await annaClient.GetAsync(Me, Ct);
        Assert.Equal(HttpStatusCode.NotFound, meAfter.StatusCode);
        using var anonymous = fixture.Api.CreateClient();
        using var loginAfter = await anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(anna.Email, anna.Password), CoreApiFactory.Json, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, loginAfter.StatusCode);

        Assert.Equal(0, await CountRowsAsync("kitchen_settings", "user_id", anna.Id));
        Assert.Equal(1, await CountRowsAsync("users", "id", boris.Id));
        Assert.Equal(1, await CountRowsAsync("kitchen_settings", "user_id", boris.Id));
    }

    [Fact]
    public async Task DeleteWithWrongPassword_Returns403AndKeepsAccount()
    {
        var anna = await fixture.Api.RegisterAsync();
        using var client = await fixture.Api.CreateAuthenticatedClientAsync(anna);

        using var delete = await SendDeleteAsync(client, "wrong-password");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(1, await CountRowsAsync("users", "id", anna.Id));
    }

    [Fact]
    public async Task DeleteWithoutPassword_Returns400()
    {
        var anna = await fixture.Api.RegisterAsync();
        using var client = await fixture.Api.CreateAuthenticatedClientAsync(anna);

        using var delete = await SendDeleteAsync(client, "");

        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> SendDeleteAsync(HttpClient client, string password)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, Me)
        {
            Content = JsonContent.Create(new DeleteAccountRequest(password), options: CoreApiFactory.Json),
        };
        return client.SendAsync(request, Ct);
    }

    private async Task<long> CountRowsAsync(string table, string column, Guid id)
    {
        await using var db = await fixture.OpenAppConnectionAsync();
        await using var command = db.CreateCommand();
        // Table and column names come from the test code only, never from input.
#pragma warning disable CA2100
        command.CommandText = $"SELECT count(*) FROM {table} WHERE {column} = @id";
#pragma warning restore CA2100
        command.Parameters.AddWithValue("id", id);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }
}
