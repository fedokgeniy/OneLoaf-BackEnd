namespace OneLoaf.ServiceDefaults.Errors;

/// <summary><c>type</c> URIs of the problem details returned by OneLoaf (api-design.md §2).</summary>
public static class ProblemTypes
{
    /// <summary>Common prefix of all problem types.</summary>
    public const string BaseUri = "https://oneloaf.app/problems/";

    /// <summary>Request validation failed (400).</summary>
    public const string Validation = BaseUri + "validation";

    /// <summary>Malformed request or invalid argument (400).</summary>
    public const string BadRequest = BaseUri + "bad-request";

    /// <summary>Not allowed (403).</summary>
    public const string Forbidden = BaseUri + "forbidden";

    /// <summary>Not found or belongs to another user (404).</summary>
    public const string NotFound = BaseUri + "not-found";

    /// <summary>Conflict with existing data or with the current state (409).</summary>
    public const string Conflict = BaseUri + "conflict";

    /// <summary>Business rule violated (422).</summary>
    public const string BusinessRule = BaseUri + "business-rule";

    /// <summary>Dependency unavailable (503).</summary>
    public const string ServiceUnavailable = BaseUri + "service-unavailable";

    /// <summary>Unexpected server error (500).</summary>
    public const string Internal = BaseUri + "internal";
}
