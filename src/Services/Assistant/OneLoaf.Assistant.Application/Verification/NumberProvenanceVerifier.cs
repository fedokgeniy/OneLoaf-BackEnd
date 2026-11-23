using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace OneLoaf.Assistant.Application.Verification;

/// <summary>Outcome of the number check.</summary>
/// <param name="IsValid">Every number in the text comes from a tool result or the baker's message.</param>
/// <param name="Unverified">Numbers that have no source.</param>
public sealed record VerificationResult(bool IsValid, IReadOnlyList<string> Unverified);

/// <summary>
/// Checks that every number the model writes was produced by a backend tool in this turn or written by the baker
/// (US-35): quantities, money, minutes, times and dates must not be invented or computed by the model.
/// </summary>
public static partial class NumberProvenanceVerifier
{
    private const int MaxOrdinal = 10;
    private const int MaxFractionDigits = 2;

    /// <summary>Checks <paramref name="text"/> against the tool outputs and the baker's messages.</summary>
    public static VerificationResult Verify(string text, IEnumerable<string> toolOutputs, IEnumerable<string> userMessages, TimeZoneInfo timeZone)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(toolOutputs);
        ArgumentNullException.ThrowIfNull(userMessages);
        ArgumentNullException.ThrowIfNull(timeZone);

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 1; i <= MaxOrdinal; i++)
        {
            allowed.Add(i.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var output in toolOutputs)
        {
            AddFromJson(output, allowed, timeZone);
        }

        foreach (var message in userMessages)
        {
            AddFromText(message, allowed);
        }

        var unverified = ExtractTokens(text).Where(token => !allowed.Contains(token)).Distinct(StringComparer.Ordinal).ToList();
        return new VerificationResult(unverified.Count == 0, unverified);
    }

    /// <summary>Replaces unverified numbers with a marker so that no invented number reaches the baker.</summary>
    public static string Redact(string text, IReadOnlyCollection<string> unverified)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(unverified);
        return unverified.Count == 0 ? text : TokenRegex().Replace(text, m => unverified.Contains(Normalize(m.Value)) ? "[?]" : m.Value);
    }

    /// <summary>Numeric tokens of a text, normalized (dates as yyyy-MM-dd, times as HH:mm, numbers without trailing zeros).</summary>
    internal static IEnumerable<string> ExtractTokens(string text) => TokenRegex().Matches(text).Select(m => Normalize(m.Value));

    private static void AddFromJson(string json, HashSet<string> allowed, TimeZoneInfo timeZone)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            Walk(document.RootElement, allowed, timeZone);
        }
        catch (JsonException)
        {
            AddFromText(json, allowed);
        }
    }

    private static void Walk(JsonElement element, HashSet<string> allowed, TimeZoneInfo timeZone)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    Walk(property.Value, allowed, timeZone);
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, allowed, timeZone);
                }

                break;
            case JsonValueKind.Number:
                AddNumber(element.GetDecimal(), allowed);
                break;
            case JsonValueKind.String:
                var value = element.GetString() ?? string.Empty;
                if (value.Length >= 20 && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
                {
                    AddInstant(instant, allowed, timeZone);
                }
                else
                {
                    AddFromText(value, allowed);
                }

                break;
        }
    }

    private static void AddInstant(DateTimeOffset instant, HashSet<string> allowed, TimeZoneInfo timeZone)
    {
        foreach (var local in new[] { instant, instant.ToUniversalTime(), TimeZoneInfo.ConvertTime(instant, timeZone) })
        {
            allowed.Add(local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            allowed.Add(local.ToString("HH:mm", CultureInfo.InvariantCulture));
            allowed.Add(local.Day.ToString(CultureInfo.InvariantCulture));
            allowed.Add(local.Year.ToString(CultureInfo.InvariantCulture));
            var hour12 = local.Hour % 12 == 0 ? 12 : local.Hour % 12;
            allowed.Add($"{hour12}:{local.Minute:00}");
        }
    }

    private static void AddFromText(string text, HashSet<string> allowed)
    {
        foreach (var token in ExtractTokens(text))
        {
            allowed.Add(token);
            if (decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                AddNumber(number, allowed);
            }
        }
    }

    /// <summary>A number and its obvious presentations: rounded to 0-2 decimals and grams written as kilograms.</summary>
    private static void AddNumber(decimal number, HashSet<string> allowed)
    {
        for (var digits = 0; digits <= MaxFractionDigits; digits++)
        {
            allowed.Add(Format(Math.Round(number, digits, MidpointRounding.AwayFromZero)));
        }

        allowed.Add(Format(number));
        if (Math.Abs(number) >= 1000)
        {
            AddNumber(number / 1000, allowed);
        }
    }

    private static string Normalize(string token)
    {
        if (token.Contains(':', StringComparison.Ordinal))
        {
            var parts = token.Split(':');
            return $"{int.Parse(parts[0], CultureInfo.InvariantCulture)}:{parts[1]}" is var t && parts[0].Length == 2 && parts[0][0] == '0'
                ? $"{parts[0]}:{parts[1]}"
                : t;
        }

        if (token.Length == 10 && token[4] == '-')
        {
            return token;
        }

        return decimal.TryParse(token.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? Format(value)
            : token;
    }

    private static string Format(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"\d{4}-\d{2}-\d{2}|\d{1,2}:\d{2}|(?<![\w.,])\d+(?:[.,]\d+)?")]
    private static partial Regex TokenRegex();
}
