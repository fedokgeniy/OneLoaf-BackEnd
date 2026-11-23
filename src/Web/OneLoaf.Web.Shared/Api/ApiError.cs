namespace OneLoaf.Web.Shared.Api;

/// <summary>Kind of an API error, taken from the ProblemDetails <c>type</c> (docs/api/guides/errors.md).</summary>
public enum ApiErrorKind
{
    /// <summary>Unrecognised problem type.</summary>
    Unknown,

    /// <summary><c>validation</c> 400: field errors in <see cref="ApiError.FieldErrors"/>.</summary>
    Validation,

    /// <summary><c>bad-request</c> 400: malformed request, a client bug.</summary>
    BadRequest,

    /// <summary><c>unauthorized</c> 401: missing or expired token, or wrong credentials at login.</summary>
    Unauthorized,

    /// <summary><c>forbidden</c> 403: role not sufficient or wrong password on account deletion.</summary>
    Forbidden,

    /// <summary><c>not-found</c> 404: missing or owned by another user.</summary>
    NotFound,

    /// <summary><c>conflict</c> 409: duplicate, object in use or wrong state.</summary>
    Conflict,

    /// <summary><c>business-rule</c> 422: a domain rule named in <see cref="ApiError.Detail"/>.</summary>
    BusinessRule,

    /// <summary><c>too-many-requests</c> 429: wait <see cref="ApiError.RetryAfter"/>.</summary>
    RateLimited,

    /// <summary><c>service-unavailable</c> 503: a dependency is down; retry after <see cref="ApiError.RetryAfter"/>.</summary>
    Unavailable,

    /// <summary><c>internal</c> 500: unexpected server error.</summary>
    Server,

    /// <summary>No HTTP response: offline, DNS, CORS or timeout.</summary>
    Network,
}

/// <summary>A failed API call in a form the UI can show (api-client.md §3).</summary>
/// <param name="Kind">Error kind used for branching.</param>
/// <param name="Status">HTTP status, or <see langword="null"/> for <see cref="ApiErrorKind.Network"/>.</param>
/// <param name="Title">Short text of the kind.</param>
/// <param name="Detail">Specific reason, safe to show to the user.</param>
/// <param name="FieldErrors">Validation messages by camelCase field path.</param>
/// <param name="CorrelationId">Id of the request in the server logs; shown in error messages.</param>
/// <param name="RetryAfter">Wait time from the <c>Retry-After</c> header (429, 503).</param>
public sealed record ApiError(
    ApiErrorKind Kind,
    int? Status,
    string Title,
    string? Detail,
    IReadOnlyDictionary<string, string[]> FieldErrors,
    string? CorrelationId,
    TimeSpan? RetryAfter)
{
    private const string TypePrefix = "https://oneloaf.app/problems/";

    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The text to show: <see cref="Detail"/> when present, otherwise <see cref="Title"/>.</summary>
    public string Message => string.IsNullOrWhiteSpace(Detail) ? Title : Detail;

    /// <summary>Builds the error of a response that carried no usable problem body.</summary>
    public static ApiError FromStatus(int status, string? correlationId = null, TimeSpan? retryAfter = null) =>
        new(KindFromStatus(status), status, DefaultTitle(KindFromStatus(status)), null, NoFieldErrors, correlationId,
            retryAfter);

    /// <summary>Builds the error from a problem details body.</summary>
    public static ApiError FromProblem(
        ProblemDetailsDto problem, int status, string? fallbackCorrelationId, TimeSpan? retryAfter)
    {
        ArgumentNullException.ThrowIfNull(problem);
        var kind = KindFromType(problem.Type) ?? KindFromStatus(status);
        var errors = problem.Errors is { Count: > 0 }
            ? new Dictionary<string, string[]>(problem.Errors, StringComparer.OrdinalIgnoreCase)
            : NoFieldErrors;
        return new ApiError(
            kind,
            problem.Status ?? status,
            string.IsNullOrWhiteSpace(problem.Title) ? DefaultTitle(kind) : problem.Title,
            problem.Detail,
            errors,
            problem.CorrelationId ?? fallbackCorrelationId,
            retryAfter);
    }

    /// <summary>Builds the error of a request that got no HTTP response.</summary>
    public static ApiError Network(string? correlationId = null) =>
        new(ApiErrorKind.Network, null, DefaultTitle(ApiErrorKind.Network), null, NoFieldErrors, correlationId, null);

    internal static ApiErrorKind? KindFromType(string? type)
    {
        if (type is null || !type.StartsWith(TypePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return type[TypePrefix.Length..] switch
        {
            "validation" => ApiErrorKind.Validation,
            "bad-request" => ApiErrorKind.BadRequest,
            "unauthorized" => ApiErrorKind.Unauthorized,
            "forbidden" => ApiErrorKind.Forbidden,
            "not-found" => ApiErrorKind.NotFound,
            "conflict" => ApiErrorKind.Conflict,
            "business-rule" => ApiErrorKind.BusinessRule,
            "too-many-requests" => ApiErrorKind.RateLimited,
            "service-unavailable" => ApiErrorKind.Unavailable,
            "internal" => ApiErrorKind.Server,
            _ => null,
        };
    }

    internal static ApiErrorKind KindFromStatus(int status) => status switch
    {
        400 => ApiErrorKind.BadRequest,
        401 => ApiErrorKind.Unauthorized,
        403 => ApiErrorKind.Forbidden,
        404 => ApiErrorKind.NotFound,
        409 => ApiErrorKind.Conflict,
        422 => ApiErrorKind.BusinessRule,
        429 => ApiErrorKind.RateLimited,
        503 => ApiErrorKind.Unavailable,
        >= 500 => ApiErrorKind.Server,
        _ => ApiErrorKind.Unknown,
    };

    private static string DefaultTitle(ApiErrorKind kind) => kind switch
    {
        ApiErrorKind.Validation => "Some fields need attention.",
        ApiErrorKind.BadRequest => "The request was not accepted.",
        ApiErrorKind.Unauthorized => "Please sign in again.",
        ApiErrorKind.Forbidden => "You are not allowed to do this.",
        ApiErrorKind.NotFound => "This item does not exist or is not yours.",
        ApiErrorKind.Conflict => "This conflicts with existing data. Reload and try again.",
        ApiErrorKind.BusinessRule => "This is not possible right now.",
        ApiErrorKind.RateLimited => "Too many requests. Please wait a moment.",
        ApiErrorKind.Unavailable => "The service is temporarily unavailable.",
        ApiErrorKind.Server => "Something went wrong on our side.",
        ApiErrorKind.Network => "No connection to the server. Check your internet connection.",
        _ => "Something went wrong.",
    };
}
