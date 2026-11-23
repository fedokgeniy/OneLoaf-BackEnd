using System.Globalization;

namespace OneLoaf.Web.Shared.Formatting;

/// <summary>
/// Display formatting only — every value comes from the API; nothing here computes a business number (rule 1).
/// Thousands use a narrow no-break space ("1 500 g"), decimals a point, as in the mockups.
/// </summary>
public static class DisplayFormat
{
    private const char ThousandsSeparator = ' ';

    private static readonly NumberFormatInfo Numbers = new()
    {
        NumberDecimalSeparator = ".",
        NumberGroupSeparator = ThousandsSeparator.ToString(),
        NumberGroupSizes = [3],
    };

    /// <summary>"1 loaf", "3 loaves".</summary>
    public static string Loaves(int count) =>
        count == 1 ? "1 loaf" : string.Create(CultureInfo.InvariantCulture, $"{count} loaves");

    /// <summary>Date in the user's culture and time zone, e.g. "23 Nov 2026".</summary>
    public static string Date(DateTimeOffset value) =>
        value.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Date, e.g. "23 Nov 2026".</summary>
    public static string Date(DateOnly value) => value.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Percent without trailing zeros, e.g. "12 %", "12.8 %".</summary>
    public static string Percent(decimal value) => Number(value, 2) + " %";

    /// <summary>Number with up to <paramref name="decimals"/> decimals and grouped thousands, e.g. "1 500", "1.875".</summary>
    public static string Number(decimal value, int decimals = 3) =>
        value.ToString("#,0." + new string('#', decimals), Numbers);

    /// <summary>Quantity with its unit code, e.g. "1 500 g", "1.875 kWh", "3 pcs".</summary>
    public static string Quantity(decimal value, string? unit) =>
        string.IsNullOrEmpty(unit) ? Number(value) : $"{Number(value)} {unit}";

    /// <summary>Weight in grams, e.g. "2 880 g".</summary>
    public static string Grams(decimal grams) => Number(grams, 0) + " g";

    /// <summary>Money amount with exactly two decimals, e.g. "6.05", or "6.05 EUR" with a currency.</summary>
    public static string Money(decimal amount, string? currency = null) =>
        amount.ToString("#,0.00", Numbers) + (string.IsNullOrEmpty(currency) ? string.Empty : " " + currency);

    /// <summary>
    /// Unit price as the API sends it per base unit; grams and millilitres are shown per kilogram / litre, which is a
    /// change of display unit only ("0.00135 /g" → "1.35 /kg").
    /// </summary>
    public static string UnitPrice(decimal price, string? unit) => unit switch
    {
        "g" => Money(price * 1000m) + " /kg",
        "ml" => Money(price * 1000m) + " /l",
        null or "" => Number(price, 4),
        _ => Money(price) + " /" + unit,
    };

    /// <summary>Duration in minutes: "45 min", "1 h 30 min", "4 h".</summary>
    public static string Duration(int minutes)
    {
        if (minutes < 60)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{minutes} min");
        }

        var hours = minutes / 60;
        var rest = minutes % 60;
        return rest == 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours} h")
            : string.Create(CultureInfo.InvariantCulture, $"{hours} h {rest} min");
    }

    /// <summary>Duration range: "15–20 min", "4–5 h", "45 min–1 h 30 min"; one value when min equals max.</summary>
    public static string DurationRange(int minMinutes, int maxMinutes)
    {
        if (minMinutes == maxMinutes)
        {
            return Duration(minMinutes);
        }

        if (maxMinutes < 60)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{minMinutes}–{maxMinutes} min");
        }

        if (minMinutes % 60 == 0 && maxMinutes % 60 == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{minMinutes / 60}–{maxMinutes / 60} h");
        }

        return $"{Duration(minMinutes)}–{Duration(maxMinutes)}";
    }

    /// <summary>Local clock time, e.g. "08:15".</summary>
    public static string Time(DateTimeOffset value) =>
        value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>
    /// Time window, e.g. "23:05–02:20 next day" relative to <paramref name="reference"/>; the day is named when it is not
    /// the reference day.
    /// </summary>
    public static string TimeWindow(DateTimeOffset from, DateTimeOffset to, DateTimeOffset reference)
    {
        var text = $"{Time(from)}–{Time(to)}";
        var days = to.ToLocalTime().Date - reference.ToLocalTime().Date;
        return days.Days switch
        {
            0 => text,
            1 => text + " next day",
            _ => $"{text} ({Date(to)})",
        };
    }
}
