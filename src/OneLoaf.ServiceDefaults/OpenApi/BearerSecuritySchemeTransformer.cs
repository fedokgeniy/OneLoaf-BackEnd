using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using OneLoaf.ServiceDefaults.Security;

namespace OneLoaf.ServiceDefaults.OpenApi;

/// <summary>
/// Declares the bearer (JWT) security scheme and requires it on every operation that is not anonymous, so Swagger UI
/// shows the lock and the "Authorize" button.
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    /// <summary>Name of the security scheme in the document.</summary>
    public const string SchemeName = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>Appended to the description of operations that need the admin role.</summary>
    public const string AdminNote = "Requires the admin role.";

    private const string BearerFormat = "JWT";
    private const string HttpScheme = "bearer";

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = HttpScheme,
            BearerFormat = BearerFormat,
            In = ParameterLocation.Header,
            Description = "Access token issued by POST /api/v1/auth/login of the Core API.",
        };
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var isAnonymous = metadata.OfType<IAllowAnonymous>().Any();
        if (metadata.OfType<IAuthorizeData>().Any(a => a.Policy == Policies.Admin))
        {
            operation.Description = $"{operation.Description} {AdminNote}".Trim();
        }

        if (!isAnonymous)
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(SchemeName, context.Document)] = [],
            });
        }

        return Task.CompletedTask;
    }
}
