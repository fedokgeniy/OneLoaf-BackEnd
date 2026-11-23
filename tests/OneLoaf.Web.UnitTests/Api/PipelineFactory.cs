using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Abstractions;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Api.Handlers;
using OneLoaf.Web.Shared.Auth;

namespace OneLoaf.Web.UnitTests.Api;

/// <summary>Builds the real handler chain over a stub, as Program.cs does with IHttpClientFactory.</summary>
internal static class PipelineFactory
{
    public static ApiJsonOptions Json { get; } = new([SharedJsonContext.Default]);

    public static (ApiClient Client, AuthEvents Events) Create(StubHttpHandler stub, string? bearer = null)
    {
        var events = new AuthEvents();
        var correlation = new CorrelationIdHandler();
        var problems = new ProblemDetailsHandler(events, Json, TimeProvider.System);
        var bearerHandler = new FixedBearerHandler(bearer);
        var logging = new LoggingHandler(NullLogger<LoggingHandler>.Instance);
        correlation.InnerHandler = bearerHandler;
        bearerHandler.InnerHandler = problems;
        problems.InnerHandler = logging;
        logging.InnerHandler = stub;
        var http = new HttpClient(correlation) { BaseAddress = new Uri("http://localhost:5080/") };
        return (new ApiClient(http, Json, NullLogger<ApiClient>.Instance), events);
    }

    /// <summary>Stands in for AuthTokenHandler without a TokenStore (JS interop).</summary>
    private sealed class FixedBearerHandler(string? token) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (token is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }

            return base.SendAsync(request, cancellationToken);
        }
    }
}
