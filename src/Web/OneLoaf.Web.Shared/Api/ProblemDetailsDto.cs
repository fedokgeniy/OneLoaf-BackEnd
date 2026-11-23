namespace OneLoaf.Web.Shared.Api;

/// <summary>RFC 9457 problem details body returned by every OneLoaf service for a non-2xx response.</summary>
public sealed record ProblemDetailsDto(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? Instance,
    IReadOnlyDictionary<string, string[]>? Errors,
    string? TraceId,
    string? CorrelationId);
