namespace OneLoaf.ServiceDefaults.Constants;

/// <summary>Infrastructure routes mapped by <c>MapServiceDefaults</c> in every service.</summary>
public static class Routes
{
    /// <summary>Base path of the business endpoints of every service (api-design.md §1).</summary>
    public const string ApiV1 = "/api/v1";

    /// <summary>Liveness: the process is running. Used by the Docker HEALTHCHECK.</summary>
    public const string HealthLive = "/health/live";

    /// <summary>Readiness: the dependencies (database, broker, cache) are reachable.</summary>
    public const string HealthReady = "/health/ready";

    /// <summary>Prometheus scraping endpoint.</summary>
    public const string Metrics = "/metrics";

    /// <summary>OpenAPI document route template.</summary>
    public const string OpenApiDocument = "/openapi/{documentName}.json";

    /// <summary>Route prefix of Swagger UI.</summary>
    public const string SwaggerUiPrefix = "swagger";

    /// <summary>
    /// OpenAPI document as seen from Swagger UI. Relative, so it also works behind the reverse proxy prefix
    /// (<c>/core/swagger</c> → <c>/core/openapi/v1.json</c>).
    /// </summary>
    public const string SwaggerUiDocumentUrl = "../openapi/v1.json";

    /// <summary>Name of the single OpenAPI document of a service.</summary>
    public const string OpenApiDocumentName = "v1";
}
