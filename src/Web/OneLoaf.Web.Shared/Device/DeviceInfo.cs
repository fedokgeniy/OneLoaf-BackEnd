namespace OneLoaf.Web.Shared.Device;

/// <summary>Window size classes of the design system (layout-and-devices.md).</summary>
public enum SizeClass
{
    /// <summary>Not reported yet: CSS alone decides the layout.</summary>
    Unknown,

    /// <summary>&lt; 17.5em: watch, folded cover screen.</summary>
    Watch,

    /// <summary>&lt; 37.5em: phone portrait.</summary>
    Compact,

    /// <summary>37.5–64em and phone landscape: tablet.</summary>
    Medium,

    /// <summary>64–90em: laptop, desktop.</summary>
    Expanded,

    /// <summary>90–160em: large desktop.</summary>
    Large,

    /// <summary>≥ 160em: ultrawide.</summary>
    Ultra,
}

/// <summary>What <c>device.js</c> reports about the window and the user's system preferences.</summary>
public sealed record DeviceInfo(
    SizeClass SizeClass,
    bool Landscape,
    bool CoarsePointer,
    bool CanHover,
    bool PrefersDark,
    bool PrefersReducedMotion,
    bool TwoSegments,
    double PixelRatio)
{
    /// <summary>Before the first report.</summary>
    public static DeviceInfo Unknown { get; } = new(SizeClass.Unknown, false, false, true, false, false, false, 1);

    /// <summary>Parses the size class name sent by <c>device.js</c>.</summary>
    public static SizeClass ParseSizeClass(string? name) =>
        Enum.TryParse<SizeClass>(name, ignoreCase: true, out var value) ? value : SizeClass.Unknown;
}
