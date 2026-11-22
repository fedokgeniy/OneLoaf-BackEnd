using Microsoft.AspNetCore.Http;
using OneLoaf.ServiceDefaults.Constants;
using OneLoaf.ServiceDefaults.Correlation;

namespace OneLoaf.CoreApi.UnitTests.ServiceDefaults;

public sealed class CorrelationIdTests
{
    [Theory]
    [InlineData("c0a8-0001", true)]
    [InlineData("abc_DEF.123:x", true)]
    [InlineData("", false)]
    [InlineData("has space", false)]
    [InlineData("line\nbreak", false)]
    [InlineData("<script>", false)]
    public void IsValid_ChecksCharacters(string value, bool expected) =>
        Assert.Equal(expected, CorrelationIdMiddleware.IsValid(value));

    [Fact]
    public void IsValid_TooLong_ReturnsFalse() =>
        Assert.False(CorrelationIdMiddleware.IsValid(new string('a', CorrelationIdMiddleware.MaxLength + 1)));

    [Fact]
    public async Task InvokeAsync_ValidIncomingId_IsKeptForTheRequest()
    {
        var correlation = new CorrelationContext();
        string? seen = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            seen = correlation.CorrelationId;
            return Task.CompletedTask;
        }, correlation);
        var context = new DefaultHttpContext();
        context.Request.Headers[Headers.CorrelationId] = "client-id-42";

        await middleware.InvokeAsync(context);

        Assert.Equal("client-id-42", seen);
        Assert.Equal("client-id-42", context.TraceIdentifier);
    }

    [Fact]
    public async Task InvokeAsync_InvalidIncomingId_IsReplaced()
    {
        var correlation = new CorrelationContext();
        string? seen = null;
        var middleware = new CorrelationIdMiddleware(_ =>
        {
            seen = correlation.CorrelationId;
            return Task.CompletedTask;
        }, correlation);
        var context = new DefaultHttpContext();
        context.Request.Headers[Headers.CorrelationId] = "bad id with spaces";

        await middleware.InvokeAsync(context);

        Assert.NotNull(seen);
        Assert.NotEqual("bad id with spaces", seen);
        Assert.Equal(32, seen.Length);
    }

    [Fact]
    public async Task DelegatingHandler_AddsCurrentCorrelationId()
    {
        var correlation = new CorrelationContext();
        correlation.SetCorrelationId("outgoing-1");
        using var inner = new RecordingHandler();
        using var handler = new CorrelationIdDelegatingHandler(correlation) { InnerHandler = inner };
        using var client = new HttpClient(handler);

        using var response = await client.GetAsync(new Uri("http://core-api/health/live"),
            TestContext.Current.CancellationToken);

        Assert.Equal(["outgoing-1"], inner.LastRequest!.Headers.GetValues(Headers.CorrelationId));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
