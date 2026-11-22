using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using OneLoaf.CoreApi.Application.Account;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.Domain;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

[Collection(ApiTestGroup.Name)]
public sealed class AuthTests(ApiFixture fixture)
{
    private const string Register = "/api/v1/auth/register";
    private const string Login = "/api/v1/auth/login";
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task US01_RegisterUnregisteredEmail_Returns201AndCreatesEmptyWorkspace()
    {
        using var client = fixture.Api.CreateClient();
        var email = $"Anna-{Guid.NewGuid():N}@Example.com";

        using var response = await client.PostAsJsonAsync(Register,
            new RegisterRequest(email, "secret-pass-1", "Anna"), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/api/v1/me", response.Headers.Location?.OriginalString);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(CoreApiFactory.Json, Ct);
        Assert.NotNull(body);

        await using var db = await fixture.OpenAppConnectionAsync();
        await using var command = db.CreateCommand();
        command.CommandText = """
            SELECT u.email, u.normalized_email, u.role, k.currency, k.time_zone, k.packaging_cost_per_loaf,
                   k.loss_rate_percent, k.oven_power_kw IS NULL
            FROM users u JOIN kitchen_settings k ON k.user_id = u.id
            WHERE u.id = @id
            """;
        command.Parameters.AddWithValue("id", body.UserId);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        Assert.Equal(email, reader.GetString(0));
        Assert.Equal(email.ToUpperInvariant(), reader.GetString(1));
        Assert.Equal("user", reader.GetString(2));
        Assert.Equal(KitchenSettingsDefaults.Currency, reader.GetString(3));
        Assert.Equal(KitchenSettingsDefaults.TimeZone, reader.GetString(4));
        Assert.Equal(0m, reader.GetDecimal(5));
        Assert.Equal(0m, reader.GetDecimal(6));
        Assert.True(reader.GetBoolean(7));
    }

    [Fact]
    public async Task RegisterExistingEmailInAnotherCase_Returns409()
    {
        var user = await fixture.Api.RegisterAsync();
        using var client = fixture.Api.CreateClient();

        using var response = await client.PostAsJsonAsync(Register,
            new RegisterRequest(user.Email.ToUpperInvariant(), "secret-pass-2", "Copy"), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RegisterInvalidInput_Returns400WithFieldErrors()
    {
        using var client = fixture.Api.CreateClient();

        using var response = await client.PostAsJsonAsync(Register,
            new RegisterRequest("not-an-email", "short", " "), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("email", out _));
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.True(errors.TryGetProperty("displayName", out _));
    }

    [Fact]
    public async Task US02_LoginWithCorrectCredentials_ReturnsTokenThatOpensMe()
    {
        var user = await fixture.Api.RegisterAsync("Anna");
        using var client = fixture.Api.CreateClient();

        using var response = await client.PostAsJsonAsync(Login, new LoginRequest(user.Email, user.Password),
            CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(CoreApiFactory.Json, Ct);
        Assert.NotNull(login);
        Assert.Equal(user.Id, login.User.Id);
        Assert.Equal(UserRole.User, login.User.Role);
        Assert.NotNull(login.User.LastLoginAt);
        Assert.True(login.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(55));

        using var me = await fixture.Api.CreateAuthenticatedClientAsync(user);
        var profile = await me.GetFromJsonAsync<UserProfileDto>("/api/v1/me", CoreApiFactory.Json, Ct);
        Assert.Equal(user.Id, profile!.Id);
        Assert.Equal("Anna", profile.DisplayName);
    }

    [Fact]
    public async Task US02_WrongPasswordAndUnknownEmail_GiveTheSame401()
    {
        var user = await fixture.Api.RegisterAsync();
        using var client = fixture.Api.CreateClient();

        using var wrongPassword = await client.PostAsJsonAsync(Login,
            new LoginRequest(user.Email, "wrong-password"), CoreApiFactory.Json, Ct);
        using var unknownEmail = await client.PostAsJsonAsync(Login,
            new LoginRequest($"nobody-{Guid.NewGuid():N}@example.com", "wrong-password"), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await ProblemWithoutVolatileFields(wrongPassword), await ProblemWithoutVolatileFields(unknownEmail));
    }

    [Fact]
    public async Task Logout_WithToken_Returns204()
    {
        var user = await fixture.Api.RegisterAsync();
        using var client = await fixture.Api.CreateAuthenticatedClientAsync(user);

        using var response = await client.PostAsync("/api/v1/auth/logout", content: null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The problem body without the per-request fields (correlation and trace ids).</summary>
    private static async Task<string> ProblemWithoutVolatileFields(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        var fields = document.RootElement.EnumerateObject()
            .Where(p => p.Name is not ("correlationId" or "traceId"))
            .Select(p => $"{p.Name}={p.Value.GetRawText()}");
        return string.Join(";", fields);
    }
}
