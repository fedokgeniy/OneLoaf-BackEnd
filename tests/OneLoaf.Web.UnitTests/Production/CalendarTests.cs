using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using OneLoaf.Web.Features.Production.Pages;
using OneLoaf.Web.Features.Production.State;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Contracts.Notification;

namespace OneLoaf.Web.UnitTests.Production;

public sealed class CalendarTests : BunitContext
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 9, 0, 0, TimeZoneInfo.Local.GetUtcOffset(new DateTime(2026, 11, 25)));
    private readonly Api _api = new();

    public CalendarTests() => this.AddProductionFakes(_api, new FakeTimeProvider(Now));

    [Theory]
    [InlineData("2026-W48", 2026, 48)]
    [InlineData("2026-W53", 2026, 53)]
    public void Parse_IsoWeek_RoundTrips(string code, int year, int week)
    {
        var parsed = CalendarWeek.Parse(code);

        Assert.Equal(new CalendarWeek(year, week), parsed);
        Assert.Equal(code, parsed!.Code);
        Assert.Equal(DayOfWeek.Monday, parsed.Start.DayOfWeek);
    }

    [Fact]
    public void Shift_AcrossNewYear_GoesToTheNextIsoWeek()
    {
        Assert.Equal(new CalendarWeek(2027, 1), new CalendarWeek(2026, 53).Shift(1));
        Assert.Null(CalendarWeek.Parse("2026-W99"));
    }

    [Fact]
    public void Render_Week_ShowsBatchOnItsDayAndWarnsWhenRemindersFail()
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo("batches");

        var cut = Render<CalendarPage>();

        cut.WaitForAssertion(() => Assert.Contains("Saturday market", cut.Find(".calendar__day--today").TextContent));
        Assert.Contains("Reminders are not available right now.", cut.Find(".ol-alert").TextContent);
        Assert.Equal("23 Nov – 29 Nov 2026", cut.Find(".calendar__week").TextContent);
        Assert.Equal(Now.AddDays(-2).Date, _api.From!.Value.Date);

        cut.Find("button[aria-label='Next week']").Click();

        Assert.EndsWith("batches?week=2026-W49", Services.GetRequiredService<NavigationManager>().Uri, StringComparison.Ordinal);
    }

    private sealed class Api : ProductionApiStub
    {
        public DateTimeOffset? From { get; private set; }

        public override Task<ApiResult<PagedResult<BatchSummaryDto>>> ListBatchesAsync(DateTimeOffset? from, DateTimeOffset? to, string? status, CancellationToken ct = default)
        {
            From = from;
            return Ok(new PagedResult<BatchSummaryDto>(
                [new BatchSummaryDto(Guid.NewGuid(), "Saturday market", BatchStatuses.Planned, Now.AddHours(2), Now.AddHours(17), Now.AddHours(20), 3)], 1, 100, 1));
        }

        public override Task<ApiResult<IReadOnlyList<ReminderDto>>> ListRemindersAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken ct = default) =>
            Task.FromResult(ApiResult<IReadOnlyList<ReminderDto>>.Fail(ApiError.FromStatus(503, "n-1")));
    }
}
