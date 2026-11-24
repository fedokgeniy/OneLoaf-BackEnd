namespace OneLoaf.Web.Shared.Ui;

/// <summary>One row of <see cref="OlBarChart"/>: the API value, its formatted text and an optional detail for the hover.</summary>
public sealed record BarDatum(string Label, decimal Value, string ValueText, string? Detail = null);
