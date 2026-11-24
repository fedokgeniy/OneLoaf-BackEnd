using System.Globalization;
using System.Text;

namespace OneLoaf.Web.Shared.Api;

/// <summary>Builds relative API URLs with an escaped, culture-invariant query string; null values are left out.</summary>
public static class Url
{
    /// <summary>E.g. <c>Url.Build("api/v1/recipes", ("search", "rye"), ("page", 2))</c> → <c>api/v1/recipes?search=rye&amp;page=2</c>.</summary>
    public static string Build(string path, params (string Name, object? Value)[] query)
    {
        ArgumentNullException.ThrowIfNull(query);
        var builder = new StringBuilder(path);
        var separator = '?';
        foreach (var (name, value) in query)
        {
            var text = Format(value);
            if (text is null)
            {
                continue;
            }

            builder.Append(separator).Append(Uri.EscapeDataString(name)).Append('=').Append(Uri.EscapeDataString(text));
            separator = '&';
        }

        return builder.ToString();
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        string s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(),
        bool b => b ? "true" : "false",
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTimeOffset t => t.ToString("O", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
