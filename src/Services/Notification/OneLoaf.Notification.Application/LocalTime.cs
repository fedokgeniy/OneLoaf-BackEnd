namespace OneLoaf.Notification.Application;

/// <summary>Formats times in the baker's time zone.</summary>
public static class LocalTime
{
    private const string Pattern = "HH:mm, d MMM";

    /// <summary>The instant in the time zone (UTC when the zone is unknown), e.g. <c>06:30, 14 Dec</c>.</summary>
    public static string Format(DateTimeOffset instant, string timeZone)
    {
        var local = TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone)
            ? TimeZoneInfo.ConvertTime(instant, zone)
            : instant.ToUniversalTime();
        return local.ToString(Pattern, System.Globalization.CultureInfo.InvariantCulture);
    }
}
