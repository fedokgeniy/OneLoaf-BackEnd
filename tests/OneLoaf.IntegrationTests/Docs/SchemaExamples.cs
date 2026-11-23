using System.Text.Json.Nodes;

namespace OneLoaf.IntegrationTests.Docs;

/// <summary>Builds an example value from an OpenAPI schema (used when no example was recorded).</summary>
public sealed class SchemaExamples(JsonNode spec)
{
    private const int MaxDepth = 6;

    /// <summary>Example of the schema.</summary>
    public JsonNode? For(JsonNode? schema, int depth = 0)
    {
        if (schema is null || depth > MaxDepth)
        {
            return null;
        }

        if (schema["$ref"]?.GetValue<string>() is { } reference)
        {
            return For(Resolve(reference), depth + 1);
        }

        if (schema["enum"] is JsonArray values && values.Count > 0)
        {
            return values.FirstOrDefault(v => v is not null)?.DeepClone();
        }

        if (schema["oneOf"] is JsonArray oneOf)
        {
            return For(oneOf.FirstOrDefault(s => s?["type"]?.ToString() != "null"), depth + 1);
        }

        var type = MainType(schema);
        var format = schema["format"]?.GetValue<string>();
        return type switch
        {
            "object" or null when schema["properties"] is JsonObject properties => ObjectExample(properties, depth),
            "object" when schema["additionalProperties"] is JsonNode additional => new JsonObject { ["key"] = For(additional, depth + 1) },
            "array" => new JsonArray(For(schema["items"], depth + 1)),
            "string" => format switch
            {
                "uuid" => "3f2b8c1e-7a4d-4c2b-9e1f-0a1b2c3d4e5f",
                "date-time" => "2026-12-05T08:00:00+02:00",
                "date" => "2026-12-05",
                "email" => "baker@example.com",
                _ => "string",
            },
            "integer" => 1,
            "number" => 1.5,
            "boolean" => true,
            _ => null,
        };
    }

    /// <summary>The schema a <c>$ref</c> points to.</summary>
    public JsonNode? Resolve(string reference) => spec["components"]?["schemas"]?[reference[(reference.LastIndexOf('/') + 1)..]];

    /// <summary>The non-null type of a schema (OpenAPI 3.1 allows type arrays).</summary>
    public static string? MainType(JsonNode schema) => schema["type"] switch
    {
        JsonArray types => types.Select(t => t?.GetValue<string>()).FirstOrDefault(t => t is not null and not "null"),
        JsonValue value => value.GetValue<string>(),
        _ => null,
    };

    private JsonObject ObjectExample(JsonObject properties, int depth)
    {
        var result = new JsonObject();
        foreach (var (name, property) in properties)
        {
            result[name] = For(property, depth + 1);
        }

        return result;
    }
}
