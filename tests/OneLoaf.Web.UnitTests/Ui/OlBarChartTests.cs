using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.UnitTests.Ui;

public sealed class OlBarChartTests : BunitContext
{
    [Fact]
    public void Render_Values_ScalesToTheLargestAndLabelsEveryRow()
    {
        var cut = Render<OlBarChart>(p => p
            .Add(x => x.Caption, "Bakes per recipe")
            .Add(x => x.Items, [new BarDatum("Rye", 4m, "4"), new BarDatum("Wheat", 1m, "1", "not rated yet"), new BarDatum("Spelt", 0m, "0")]));

        var rows = cut.FindAll(".ol-bars__row");
        Assert.Equal("Bakes per recipe", cut.Find("figcaption").TextContent);
        Assert.Equal(3, rows.Count);
        Assert.Equal("width: 100%", cut.FindAll(".ol-bars__bar")[0].GetAttribute("style"));
        Assert.Equal("width: 25%", cut.FindAll(".ol-bars__bar")[1].GetAttribute("style"));
        Assert.Equal("width: 0%", cut.FindAll(".ol-bars__bar")[2].GetAttribute("style"));
        Assert.Contains("not rated yet", rows[1].TextContent, StringComparison.Ordinal);
        Assert.Equal("true", cut.Find(".ol-bars__track").GetAttribute("aria-hidden"));
    }
}
