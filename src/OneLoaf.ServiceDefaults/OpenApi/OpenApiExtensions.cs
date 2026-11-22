using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using OneLoaf.ServiceDefaults.Constants;

namespace OneLoaf.ServiceDefaults.OpenApi;

/// <summary>OpenAPI document (Microsoft.AspNetCore.OpenApi) and Swagger UI of a service (requirement M6, US-45).</summary>
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
                };
                return Task.CompletedTask;
            });
            options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
            options.AddOperationTransformer<BearerSecuritySchemeTransformer>();
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
