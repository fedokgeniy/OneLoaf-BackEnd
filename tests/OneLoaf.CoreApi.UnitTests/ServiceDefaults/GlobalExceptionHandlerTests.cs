using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Net.Http.Headers;
using OneLoaf.ServiceDefaults.Correlation;
using OneLoaf.ServiceDefaults.Errors;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.CoreApi.UnitTests.ServiceDefaults;

public sealed class GlobalExceptionHandlerTests
{
    private const string CorrelationId = "test-correlation-1";
    private const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task TryHandleAsync_NotFound_WritesProblemDetailsWithCorrelationId()
    {
        var (handler, context) = Create(Environments.Production);

        var handled = await handler.TryHandleAsync(context, new NotFoundException("Recipe not found."),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.StartsWith(ProblemJson, context.Response.ContentType, StringComparison.Ordinal);
        using var body = ReadBody(context);
        var root = body.RootElement;
        Assert.Equal(ProblemTypes.NotFound, root.GetProperty("type").GetString());
        Assert.Equal("Recipe not found.", root.GetProperty("detail").GetString());
        Assert.Equal("/api/v1/recipes/1", root.GetProperty("instance").GetString());
        Assert.Equal(CorrelationId, root.GetProperty(ErrorHandlingExtensions.CorrelationIdExtension).GetString());
        Assert.False(root.TryGetProperty(GlobalExceptionHandler.ExceptionTypeExtension, out _));
    }

    [Fact]
    public async Task TryHandleAsync_ServiceUnavailable_SetsRetryAfterHeader()
    {
        var (handler, context) = Create(Environments.Production);

        await handler.TryHandleAsync(context,
            new ExternalServiceUnavailableException("Claude is unavailable.", TimeSpan.FromSeconds(12.2)),
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("13", context.Response.Headers[HeaderNames.RetryAfter].ToString());
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_WritesErrors()
    {
        var (handler, context) = Create(Environments.Production);
        var exception = new FluentValidation.ValidationException(
            [new FluentValidation.Results.ValidationFailure("Loaves", "Must be between 1 and 1000.")]);

        await handler.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        using var body = ReadBody(context);
        var errors = body.RootElement.GetProperty("errors");
        Assert.Equal("Must be between 1 and 1000.", errors.GetProperty("loaves")[0].GetString());
    }

    [Fact]
    public async Task TryHandleAsync_UnknownExceptionInDevelopment_AddsExceptionTypeButNoMessage()
    {
        var (handler, context) = Create(Environments.Development);

        await handler.TryHandleAsync(context, new InvalidOperationException("secret detail"),
            TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        using var body = ReadBody(context);
        var root = body.RootElement;
        Assert.Equal(typeof(InvalidOperationException).FullName,
            root.GetProperty(GlobalExceptionHandler.ExceptionTypeExtension).GetString());
        Assert.DoesNotContain("secret detail", root.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_RequestAborted_SetsStatusWithoutBody()
    {
        var (handler, context) = Create(Environments.Production);
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        context.RequestAborted = aborted.Token;

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(aborted.Token),
            TestContext.Current.CancellationToken);

        Assert.True(handled);
        Assert.Equal(ExceptionMapper.ClientClosedRequest, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private static (GlobalExceptionHandler Handler, DefaultHttpContext Context) Create(string environmentName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCorrelationId();
        services.AddOneLoafErrorHandling();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName));
        var provider = services.BuildServiceProvider();

        provider.GetRequiredService<ICorrelationContext>().SetCorrelationId(CorrelationId);
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/api/v1/recipes/1";
        context.Response.Body = new MemoryStream();

        var handler = new GlobalExceptionHandler(
            provider.GetRequiredService<IProblemDetailsService>(),
            provider.GetRequiredService<IHostEnvironment>(),
            NullLogger<GlobalExceptionHandler>.Instance);
        return (handler, context);
    }

    private static JsonDocument ReadBody(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
