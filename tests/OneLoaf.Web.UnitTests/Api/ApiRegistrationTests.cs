using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Api;

public sealed class ApiRegistrationTests
{
    [Fact]
    public async Task AddOneLoafApi_SameOriginConfig_ResolvesPrefixAndSendsBearerFromTokenStore()
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.OK,
            """{"id":"99cf6d64-ee83-4a4b-9f20-8cb359c15b6a","email":"a@b.c","displayName":"A","role":"user","createdAt":"2026-11-23T10:08:53+00:00","lastLoginAt":null}""");
        await using var provider = BuildServices(stub, new Dictionary<string, string?>
        {
            ["Api:Core"] = "/core/",
            ["Api:Assistant"] = "/assistant/",
            ["Api:Notification"] = "/notify",
        });
        await provider.GetRequiredService<TokenStore>().SetAsync("the-token", DateTimeOffset.UtcNow.AddHours(1));

        var result = await provider.GetRequiredService<ApiClientFactory>().Core
            .GetAsync<UserDto>("api/v1/me", Xunit.TestContext.Current.CancellationToken);

        Assert.IsType<ApiResult<UserDto>.Success>(result);
        Assert.Equal("https://oneloaf.example/core/api/v1/me", stub.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer the-token", stub.LastRequest.Headers.Authorization!.ToString());
        Assert.Equal(new Uri("https://oneloaf.example/notify/"),
            new ServiceEndpoints { Notification = "/notify" }.For(ServiceNames.Notification, new Uri("https://oneloaf.example/")));
    }

    [Fact]
    public async Task Core_MissingConfiguration_ThrowsClearMessage()
    {
        await using var provider = BuildServices(StubHttpHandler.Json(HttpStatusCode.OK, "{}"), []);

        var ex = Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ApiClientFactory>().Core);

        Assert.Contains("Api:core", ex.Message, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServices(StubHttpHandler stub, Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IJSRuntime>(new BunitJSInterop { Mode = JSRuntimeMode.Loose }.JSRuntime);
        services.AddOneLoafApi(configuration, new Uri("https://oneloaf.example/"));
        services.ConfigureAll<HttpClientFactoryOptions>(o =>
            o.HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = stub));
        return services.BuildServiceProvider();
    }
}
