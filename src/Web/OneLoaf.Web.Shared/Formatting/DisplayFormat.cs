using System.Globalization;

namespace OneLoaf.Web.Shared.Formatting;

/// <summary>Display formatting only — values come from the API; nothing here computes a business number (rule 1).</summary>
public static class DisplayFormat
{
    /// <summary>"1 loaf", "3 loaves".</summary>
    public static string Loaves(int count) =>
        count == 1 ? "1 loaf" : string.Create(CultureInfo.CurrentCulture, $"{count} loaves");

    /// <summary>Date in the user's culture and time zone, e.g. "23 Nov 2026".</summary>
    public static string Date(DateTimeOffset value) =>
        value.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture);

    /// <summary>Percent without trailing zeros, e.g. "12 %", "12.5 %".</summary>
    public static string Percent(decimal value) =>
        string.Create(CultureInfo.CurrentCulture, $"{value:0.##} %");
}
