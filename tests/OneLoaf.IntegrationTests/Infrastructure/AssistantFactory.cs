using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.Assistant.Endpoints;
using OneLoaf.Assistant.Infrastructure;

namespace OneLoaf.IntegrationTests.Infrastructure;

/// <summary>
/// The AI Assistant in-process. Claude is replaced by a scripted client (no API key, no cost); the Core API HTTP client
/// is pointed at the in-process Core API, so tools really call the Core API with the user's token.
/// </summary>
public sealed class AssistantFactory(
    IReadOnlyDictionary<string, string?> settings, IClaudeClient claude, Func<HttpMessageHandler> coreApiHandler)
    : WebApplicationFactory<AssistantEntryPoint>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(CoreApiFactory.Environment);
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(claude);
            services.AddHttpClient<ICoreApiClient, CoreApiClient>().ConfigurePrimaryHttpMessageHandler(coreApiHandler);
        });
    }
}

/// <summary>A Claude stand-in that replays a script; each step may look at the request (for example the last tool result).</summary>
public sealed class ScriptedClaude(params Func<ClaudeRequest, ClaudeResponse>[] steps) : IClaudeClient
{
    private readonly Queue<Func<ClaudeRequest, ClaudeResponse>> _steps = new(steps);

    public Task<ClaudeResponse> SendAsync(ClaudeRequest request, CancellationToken cancellationToken) =>
        _steps.Count == 0
            ? throw new InvalidOperationException("The script has no more steps.")
            : Task.FromResult(_steps.Dequeue()(request));

    /// <summary>Content of the last tool result in the request.</summary>
    public static string LastToolResult(ClaudeRequest request) =>
        request.Messages.SelectMany(m => m.Blocks).OfType<ClaudeToolResultBlock>().Last().Content;
}
