using System.Net;
using System.Text;

namespace OneLoaf.Web.UnitTests.Api;

/// <summary>Innermost handler that answers every request with a fixed response (or throws) and records the request.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }

    public static StubHttpHandler Json(HttpStatusCode status, string json, string mediaType = "application/json") =>
        new(_ => new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) });

    public static StubHttpHandler Throws(Exception exception) => new(_ => throw exception);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        return Task.FromResult(respond(request));
    }
}
