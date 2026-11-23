using OneLoaf.Web.Shared.Ui.Icons;

namespace OneLoaf.Web.Shared.Ui;

/// <summary>One option of <see cref="OlSegmented{TValue}"/>.</summary>
public sealed record SegmentOption<TValue>(TValue Value, string Text, IconName? Icon = null);
