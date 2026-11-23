using System.Text;

namespace OneLoaf.SharedKernel.Text;

/// <summary>
/// Enum values as they appear on the wire and in the database (<c>in_progress</c>). Used for query string parameters,
/// which the framework would otherwise parse by the C# member name.
/// </summary>
public static class WireEnumNames
{
    /// <summary>Parses a snake_case value; false for null, empty or unknown values.</summary>
    public static bool TryParse<T>(string? value, out T result)
        where T : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<T>())
        {
            if (string.Equals(Name(candidate), value, StringComparison.Ordinal))
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>The parsed value, or null when the text is null or unknown.</summary>
    public static T? ParseOrNull<T>(string? value)
        where T : struct, Enum =>
        TryParse(value, out T result) ? result : null;

    /// <summary>A null or known value (for validators).</summary>
    public static bool IsNullOrDefined<T>(string? value)
        where T : struct, Enum =>
        value is null || TryParse<T>(value, out _);

    /// <summary>The snake_case name of a value.</summary>
    public static string Name<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        var text = new StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                text.Append('_');
            }

            text.Append(char.ToLowerInvariant(name[i]));
        }

        return text.ToString();
    }

    /// <summary>All values, comma separated, for error messages.</summary>
    public static string Values<T>()
        where T : struct, Enum =>
        string.Join(", ", Enum.GetValues<T>().Select(Name));
}
