using System.Net;
using Fluxor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.Web.Features.Assistant;
using OneLoaf.Web.Features.Assistant.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Auth;
using OneLoaf.Web.Shared.State;
using OneLoaf.Web.UnitTests.Api;

namespace OneLoaf.Web.UnitTests.State;

public sealed class LazyFeatureRegistryTests : BunitContext
{
    [Fact]
    public void Register_LazyAssembly_AddsStateReducersAndEffectsToTheRunningStore()
    {
        var stub = StubHttpHandler.Json(HttpStatusCode.OK, "[]");
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped<OneLoaf.Web.Shared.Device.DeviceContext>();
        Services.AddSingleton(PipelineFactory.Json);
        Services.AddSingleton<IHttpClientFactory>(new Clients(stub));
        Services.AddSingleton(NullLogger<ApiClient>.Instance);
        Services.AddScoped<ApiClientFactory>();
        Services.AddScoped<LazyFeatureRegistry>();
        Services.AddFluxor(o => o.ScanAssemblies(typeof(SessionState).Assembly));
        Services.AddScoped(typeof(IState<>), typeof(LazyState<>));
        Render<Fluxor.Blazor.Web.StoreInitializer>();
        var registry = Services.GetRequiredService<LazyFeatureRegistry>();

        registry.Register(typeof(AssistantFeature).Assembly);
        registry.Register(typeof(AssistantFeature).Assembly);
        Services.GetRequiredService<IDispatcher>().Dispatch(new LoadConversations());

        var state = Services.GetRequiredService<IState<AssistantState>>();
        Assert.True(registry.IsRegistered(typeof(AssistantFeature).Assembly));
        Assert.True(SpinWait.SpinUntil(() => state.Value.Conversations is Loadable<IReadOnlyList<Shared.Contracts.Assistant.ConversationDto>>.Loaded, 3000));
        Assert.Contains("api/v1/", stub.LastRequest!.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    private sealed class Clients(StubHttpHandler stub) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(stub, disposeHandler: false) { BaseAddress = new Uri("http://localhost:5081/") };
    }
}
