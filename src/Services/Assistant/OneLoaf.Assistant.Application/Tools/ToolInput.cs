using System.Globalization;
using System.Text.Json;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary>Reads and checks tool arguments.</summary>
public static class ToolInput
{
    /// <summary>A required UUID argument.</summary>
    public static bool TryGetGuid(JsonElement input, string name, out Guid value)
    {
        value = Guid.Empty;
        return input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            && Guid.TryParse(p.GetString(), out value);
    }

    /// <summary>A required integer argument within a range.</summary>
    public static bool TryGetInt(JsonElement input, string name, int min, int max, out int value)
    {
        value = 0;
        return input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            && p.TryGetInt32(out value) && value >= min && value <= max;
    }

    /// <summary>An optional string argument.</summary>
    public static string? GetString(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    /// <summary>An optional number argument.</summary>
    public static decimal? GetDecimal(JsonElement input, string name) =>
        input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetDecimal()
            : null;

    /// <summary>A required ISO 8601 instant with an offset.</summary>
    public static bool TryGetInstant(JsonElement input, string name, out DateTimeOffset value)
    {
        value = default;
        var text = GetString(input, name);
        return text is not null && (text.Contains('+', StringComparison.Ordinal) || text.EndsWith('Z') || text.LastIndexOf('-') > 9)
            && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    /// <summary>Escapes a value for a query string.</summary>
    public static string Query(string value) => Uri.EscapeDataString(value);

    /// <summary>Formats an instant for a query string.</summary>
    public static string Query(DateTimeOffset value) => Uri.EscapeDataString(value.ToString("O", CultureInfo.InvariantCulture));

    /// <summary>Builds a JSON schema object for a tool definition.</summary>
    public static JsonElement Schema(object properties, params string[] required) =>
        JsonSerializer.SerializeToElement(new { type = "object", properties, required, additionalProperties = false });
}
