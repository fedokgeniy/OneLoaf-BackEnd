using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace OneLoaf.ServiceDefaults.OpenApi;

/// <summary>
/// Copies the XML documentation of the request and response types into the OpenAPI document: type summaries become
/// schema descriptions, property summaries (or record <c>param</c> tags) become property and query parameter
/// descriptions. The XML files are the ones published next to the service assemblies (<c>OneLoaf.*.xml</c>).
/// </summary>
public sealed partial class XmlDocumentationTransformer : IOpenApiSchemaTransformer, IOpenApiOperationTransformer
{
    private static readonly Lazy<XmlDocumentation> Documentation = new(XmlDocumentation.Load);

    /// <inheritdoc />
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);
        var docs = Documentation.Value;

        if (context.JsonPropertyInfo is { } property)
        {
            schema.Description ??= docs.Property(property.DeclaringType, property.AttributeProvider as MemberInfo, property.Name);
            return Task.CompletedTask;
        }

        var type = context.JsonTypeInfo.Type;
        schema.Description ??= docs.Type(type) ?? KnownDescription(type);
        if (schema.Properties is null)
        {
            return Task.CompletedTask;
        }

        foreach (var info in context.JsonTypeInfo.Properties)
        {
            if (!schema.Properties.TryGetValue(info.Name, out var propertySchema))
            {
                continue;
            }

            var description = docs.Property(type, info.AttributeProvider as MemberInfo, info.Name);
            switch (propertySchema)
            {
                case OpenApiSchema concrete when concrete.Description is null:
                    concrete.Description = description;
                    break;
                case OpenApiSchemaReference reference when reference.Description is null:
                    reference.Description = description;
                    break;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        if (operation.Parameters is null)
        {
            return Task.CompletedTask;
        }

        var docs = Documentation.Value;
        foreach (var parameter in operation.Parameters.OfType<OpenApiParameter>().Where(p => p.Description is null))
        {
            var api = context.Description.ParameterDescriptions
                .FirstOrDefault(d => string.Equals(d.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
            var metadata = api?.ModelMetadata;
            parameter.Description = metadata?.ContainerType is { } container && metadata.PropertyName is { } name
                ? docs.Property(container, container.GetProperty(name), name)
                : parameter.In == ParameterLocation.Path ? "Id of the resource (UUID)." : null;
        }

        return Task.CompletedTask;
    }

    private static string? KnownDescription(Type type) => type.Name switch
    {
        "ProblemDetails" => "RFC 9457 problem details: type, title, status, detail, instance, plus correlationId and traceId " +
                            "for support. Retry-After is sent as a header with 429 and 503.",
        "HttpValidationProblemDetails" => "Problem details of a 400 validation error; errors maps each invalid field (camelCase) " +
                                          "to its messages.",
        _ => null,
    };

    /// <summary>Members of the loaded XML documentation files.</summary>
    private sealed partial class XmlDocumentation(Dictionary<string, XElement> members)
    {
        private const string FilePattern = "OneLoaf.*.xml";

        public static XmlDocumentation Load()
        {
            var members = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(AppContext.BaseDirectory, FilePattern))
            {
                try
                {
                    foreach (var member in XDocument.Load(file).Descendants("member"))
                    {
                        if (member.Attribute("name")?.Value is { } name)
                        {
                            members[name] = member;
                        }
                    }
                }
                catch (System.Xml.XmlException)
                {
                    // Not a documentation file.
                }
            }

            return new XmlDocumentation(members);
        }

        public string? Type(Type type) =>
            Key(type) is { } key && members.TryGetValue("T:" + key, out var member) ? Text(member.Element("summary")) : null;

        public string? Property(Type? declaringType, MemberInfo? member, string jsonName)
        {
            if (declaringType is null || Key(declaringType) is not { } typeKey)
            {
                return null;
            }

            var clrName = member?.Name ?? jsonName;
            if (members.TryGetValue($"P:{typeKey}.{clrName}", out var property) && Text(property.Element("summary")) is { } summary)
            {
                return summary;
            }

            // Positional records document their properties with <param> on the type.
            return members.TryGetValue("T:" + typeKey, out var type)
                ? Text(type.Elements("param").FirstOrDefault(p => string.Equals(p.Attribute("name")?.Value, clrName, StringComparison.OrdinalIgnoreCase)))
                : null;
        }

        private static string? Key(Type type) =>
            (type.IsGenericType ? type.GetGenericTypeDefinition() : type).FullName?.Replace('+', '.');

        private static string? Text(XElement? element)
        {
            if (element is null)
            {
                return null;
            }

            var text = new StringBuilder();
            foreach (var node in element.Nodes())
            {
                text.Append(node switch
                {
                    XText t => t.Value,
                    XElement { Name.LocalName: "see" or "seealso" } see =>
                        see.Attribute("langword")?.Value ?? ShortName(see.Attribute("cref")?.Value) ?? see.Value,
                    XElement { Name.LocalName: "paramref" or "typeparamref" } reference => reference.Attribute("name")?.Value,
                    XElement other => other.Value,
                    _ => string.Empty,
                });
            }

            var result = Whitespace().Replace(text.ToString(), " ").Trim();
            return result.Length == 0 ? null : result;
        }

        private static string? ShortName(string? cref) => cref?[(cref.LastIndexOf('.') + 1)..];

        [GeneratedRegex(@"\s+")]
        private static partial Regex Whitespace();
    }
}
