using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace OneLoaf.Web.Shared.Api.Handlers;

/// <summary>Logs method, path, status and duration of every request. Never logs headers, query strings or bodies.</summary>
public sealed partial class LoggingHandler(ILogger<LoggingHandler> logger) : DelegatingHandler
{
    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var started = Stopwatch.GetTimestamp();
        var path = request.RequestUri?.AbsolutePath ?? string.Empty;
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            var elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (response.IsSuccessStatusCode)
            {
                LogSucceeded(logger, request.Method.Method, path, (int)response.StatusCode, elapsed);
            }
            else
            {
                request.Options.TryGetValue(ApiRequestOptions.CorrelationId, out var correlationId);
                LogFailed(logger, request.Method.Method, path, (int)response.StatusCode, elapsed, correlationId);
            }

            return response;
        }
        catch (HttpRequestException ex)
        {
            LogNetworkError(logger, request.Method.Method, path, ex.Message);
            throw;
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "HTTP {Method} {Path} → {Status} in {ElapsedMs:0} ms")]
    private static partial void LogSucceeded(ILogger logger, string method, string path, int status, double elapsedMs);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "HTTP {Method} {Path} → {Status} in {ElapsedMs:0} ms (correlation id {CorrelationId})")]
    private static partial void LogFailed(
        ILogger logger, string method, string path, int status, double elapsedMs, string? correlationId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "HTTP {Method} {Path} failed without a response: {Reason}")]
    private static partial void LogNetworkError(ILogger logger, string method, string path, string reason);
}
