using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.UnitTests.Ui;

public sealed class StageItemTests : BunitContext
{
    [Fact]
    public void Render_PendingStage_OffersStartAndRaisesItWithTheStage()
    {
        var stage = Stage(StageStatuses.Pending);
        BatchStageDto? started = null;

        var cut = Render<OlStageItem>(p => p.Add(s => s.Stage, stage).Add(s => s.Number, 2)
            .Add(s => s.OnStart, (BatchStageDto s) => started = s));
        cut.Find(".ol-stage__actions .ol-btn--primary").Click();

        Assert.Same(stage, started);
        Assert.Equal("Planned", cut.Find(".ol-badge").TextContent);
        Assert.Contains("Start bulk fermentation", cut.Find(".ol-stage__actions").TextContent);
        Assert.Contains("Skip", cut.Find(".ol-stage__actions").TextContent);
        Assert.Equal("2", cut.Find(".ol-stage__marker").TextContent.Trim());
    }

    [Fact]
    public void Render_RunningStage_OffersCompleteAndThirtyMoreMinutes()
    {
        var stage = Stage(StageStatuses.Running) with { ActualStartAt = DateTimeOffset.UnixEpoch.AddHours(1), ExtendedMinutes = 30 };
        BatchStageDto? extended = null;

        var cut = Render<OlStageItem>(p => p.Add(s => s.Stage, stage).Add(s => s.OnExtend, (BatchStageDto s) => extended = s));
        cut.FindAll(".ol-stage__actions button").Single(b => b.TextContent.Contains("+30 min", StringComparison.Ordinal)).Click();

        Assert.Same(stage, extended);
        Assert.Contains("is-running", cut.Find(".ol-stage").ClassList);
        Assert.Equal("Running", cut.Find(".ol-badge").TextContent);
        Assert.Contains("Complete bulk fermentation", cut.Find(".ol-stage__actions").TextContent);
        Assert.Contains("since", cut.Find(".ol-stage__times").TextContent);
        Assert.Contains("+30 min", cut.Find(".ol-stage__deviation").TextContent);
    }

    [Theory]
    [InlineData(StageStatuses.Done, "Done")]
    [InlineData(StageStatuses.Skipped, "Skipped")]
    public void Render_FinishedStage_ShowsTheWordAndNoActions(string status, string word)
    {
        var cut = Render<OlStageItem>(p => p.Add(s => s.Stage, Stage(status)));

        Assert.Equal(word, cut.Find(".ol-badge").TextContent);
        Assert.Empty(cut.FindAll(".ol-stage__actions"));
    }

    private static BatchStageDto Stage(string status)
    {
        var start = DateTimeOffset.UnixEpoch;
        return new BatchStageDto(Guid.NewGuid(), 2, "Bulk fermentation", "bulk_fermentation", 240, 300, start, start.AddHours(4), start.AddHours(5),
            null, null, status, 0, false, null, null);
    }
}
