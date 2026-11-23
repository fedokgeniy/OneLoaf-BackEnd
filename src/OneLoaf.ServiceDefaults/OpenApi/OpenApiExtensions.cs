using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using OneLoaf.ServiceDefaults.Constants;

namespace OneLoaf.ServiceDefaults.OpenApi;

/// <summary>OpenAPI document (Microsoft.AspNetCore.OpenApi) and Swagger UI of a service (US-45).</summary>
public static class OpenApiExtensions
{
    /// <summary>Registers the OpenAPI document with service info and the bearer scheme.</summary>
    public static IServiceCollection AddOneLoafOpenApi(this IServiceCollection services, string serviceName)
    {
        services.AddOpenApi(Routes.OpenApiDocumentName, options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = $"OneLoaf {serviceName} API",
                    Version = Routes.OpenApiDocumentName,
                    Description = "Part of OneLoaf, a web application for managing bread recipes and planning home baking. " +
                                  "Authenticate with a bearer token from POST /api/v1/auth/login of the Core API. " +
                                  "Errors are RFC 9457 problem details (application/problem+json) with a correlationId; " +
                                  "JSON uses camelCase and enums are snake_case strings. Guides: docs/api in the repository.",
                    License = new OpenApiLicense { Name = "Proprietary (bachelor project)" },
                };

                // Resolved against the document URL (/openapi/v1.json or /core/openapi/v1.json behind the proxy), so
                // "Try it out" calls the same service with or without the reverse proxy path prefix.
                document.Servers = [new OpenApiServer { Url = "..", Description = "This service" }];
                return Task.CompletedTask;
            });
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
            options.AddSchemaTransformer<XmlDocumentationTransformer>();
            options.AddOperationTransformer<XmlDocumentationTransformer>();
        });
        return services;
    }

    /// <summary>
    /// Swagger UI at <c>/swagger</c>. It serves static files without an endpoint, so it must run before the
    /// authorization middleware: the fallback policy would otherwise answer 401.
    /// </summary>
    public static IApplicationBuilder UseOneLoafSwaggerUi(this IApplicationBuilder app) =>
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = Routes.SwaggerUiPrefix;
            options.SwaggerEndpoint(Routes.SwaggerUiDocumentUrl, Routes.OpenApiDocumentName);
        });

    /// <summary>Maps the OpenAPI JSON document; it is public.</summary>
    public static WebApplication MapOneLoafOpenApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapOpenApi(Routes.OpenApiDocument).AllowAnonymous();
        return app;
    }
}
