using OneLoaf.Web.Shared.Formatting;

namespace OneLoaf.Web.UnitTests.Formatting;

public sealed class DisplayFormatTests
{
    [Theory]
    [InlineData(15, 20, "15–20 min")]
    [InlineData(240, 300, "4–5 h")]
    [InlineData(45, 90, "45 min–1 h 30 min")]
    [InlineData(45, 45, "45 min")]
    public void DurationRange_Minutes_ReadsLikeTheMockups(int min, int max, string expected) =>
        Assert.Equal(expected, DisplayFormat.DurationRange(min, max));

    [Fact]
    public void Numbers_FromTheCostExample_AreFormattedNotComputed()
    {
        Assert.Equal("1 500 g", DisplayFormat.Quantity(1500.0m, "g"));
        Assert.Equal("1.875 kWh", DisplayFormat.Quantity(1.875m, "kWh"));
        Assert.Equal("1.35 /kg", DisplayFormat.UnitPrice(0.00135m, "g"));
        Assert.Equal("6.05 EUR", DisplayFormat.Money(6.05m, "EUR"));
        Assert.Equal("2 880 g", DisplayFormat.Grams(2880.0m));
    }

    [Fact]
    public void TimeWindow_EndOnNextDay_SaysNextDay()
    {
        var start = new DateTimeOffset(2026, 11, 25, 8, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 25)));

        Assert.Equal("23:05–02:20 next day", DisplayFormat.TimeWindow(start.AddHours(15).AddMinutes(5), start.AddHours(18).AddMinutes(20), start));
    }
}
