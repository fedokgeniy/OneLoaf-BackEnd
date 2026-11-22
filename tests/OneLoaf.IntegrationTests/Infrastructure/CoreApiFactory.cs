using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OneLoaf.CoreApi.Application.Auth;
using OneLoaf.ServiceDefaults;

namespace OneLoaf.IntegrationTests.Infrastructure;

/// <summary>The Core API in-process with test configuration (set with UseSetting, so Program sees it at build time).</summary>
public sealed class CoreApiFactory(IReadOnlyDictionary<string, string?> settings) : WebApplicationFactory<Program>
{
    public const string Environment = "Testing";

    public static readonly JsonSerializerOptions Json = CreateJson();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environment);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }

    /// <summary>Registers a new user with a unique e-mail and returns its credentials.</summary>
    public async Task<TestUser> RegisterAsync(string? displayName = null)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        const string password = "secret-pass-1";
        using var client = CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest(email, password, displayName ?? "Test baker"), Json);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(Json);
        return new TestUser(body!.UserId, email, password);
    }

    /// <summary>Logs in and returns a client that sends the access token.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(TestUser user)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(user.Email, user.Password), Json);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    private static JsonSerializerOptions CreateJson()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Extensions.ConfigureJson(options);
        return options;
    }
}

public sealed record TestUser(Guid Id, string Email, string Password);
