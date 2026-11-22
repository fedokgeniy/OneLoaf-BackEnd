using Microsoft.Extensions.Logging;

namespace OneLoaf.ServiceDefaults.Errors;

/// <summary>How an exception is turned into an HTTP response.</summary>
/// <param name="StatusCode">HTTP status code.</param>
/// <param name="Type">Problem type URI (<see cref="ProblemTypes"/>).</param>
/// <param name="Title">Short client-safe summary.</param>
/// <param name="Detail">Client-safe explanation, or null. Never SQL, constraint internals or stack traces.</param>
/// <param name="LogLevel">Level of the log entry written by the handler.</param>
/// <param name="WriteBody">False for a cancelled request: nobody reads the response.</param>
/// <param name="RetryAfter">Value of the <c>Retry-After</c> header, if any.</param>
/// <param name="Errors">Validation errors by camelCase property name, if any.</param>
/// <param name="SqlState">SQLSTATE of the database error, for the log only.</param>
public sealed record ProblemMapping(
    int StatusCode,
    string Type,
    string Title,
    string? Detail,
    LogLevel LogLevel,
    bool WriteBody = true,
    TimeSpan? RetryAfter = null,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    string? SqlState = null);
