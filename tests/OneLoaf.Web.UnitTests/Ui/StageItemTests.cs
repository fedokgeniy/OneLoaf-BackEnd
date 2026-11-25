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
        Assert.Equal("Start", cut.FindAll(".ol-stage__actions button")[0].TextContent.Trim());
        Assert.Contains("Skip", cut.Find(".stage-adjust__more").TextContent);

        // Nothing is chosen on the slider of a pending stage, so Adjust waits.
        Assert.True(cut.FindAll(".ol-stage__actions button")[1].HasAttribute("disabled"));
        Assert.Equal("2", cut.Find(".ol-stage__marker").TextContent.Trim());
    }

    [Fact]
    public void Render_RunningStage_OffersCompleteAndAdjustsByTheSliderMinutes()
    {
        var stage = Stage(StageStatuses.Running) with { ActualStartAt = DateTimeOffset.UnixEpoch.AddHours(1), ExtendedMinutes = 30 };
        StageAdjustment? adjusted = null;
        BatchStageDto? completed = null;

        var cut = Render<OlStageItem>(p => p.Add(s => s.Stage, stage)
            .Add(s => s.OnAdjust, (StageAdjustment a) => adjusted = a)
            .Add(s => s.OnComplete, (BatchStageDto s) => completed = s));
        var buttons = cut.FindAll(".ol-stage__actions button");

        // Complete carries no stage name and stands first; Adjust is second and uses the slider (half an hour by default).
        Assert.Equal("Complete", buttons[0].TextContent.Trim());
        Assert.Equal("Adjust", buttons[1].TextContent.Trim());
        buttons[0].Click();
        Assert.Same(stage, completed);

        cut.Find(".stage-adjust__slider").Input("45");
        Assert.Contains("+45 min", cut.Find(".stage-adjust__value").TextContent);
        cut.FindAll(".ol-stage__actions button")[1].Click();
        Assert.Equal(new StageAdjustment(stage, 45), adjusted);

        Assert.Contains("is-running", cut.Find(".ol-stage").ClassList);
        Assert.Equal("Running", cut.Find(".ol-badge").TextContent);
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
