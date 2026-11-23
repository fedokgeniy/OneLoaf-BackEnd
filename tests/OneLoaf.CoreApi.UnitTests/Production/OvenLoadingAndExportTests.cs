using OneLoaf.CoreApi.Application.Calculations;
using OneLoaf.CoreApi.Application.Export;

namespace OneLoaf.CoreApi.UnitTests.Production;

public sealed class OvenLoadingAndExportTests
{
    [Fact]
    public void US19_SameOvenTimeSharesRounds_LargerQuantitiesFirst_SplitOnlyWhenFull()
    {
        var white = Guid.NewGuid();
        var rye = Guid.NewGuid();
        var brioche = Guid.NewGuid();

        var plan = OvenLoadingPlanner.Plan(
            [new OvenLoad(white, "White", 3, 45), new OvenLoad(rye, "Rye", 2, 45), new OvenLoad(brioche, "Brioche", 2, 30)], capacity: 4);

        Assert.Equal(7, plan.TotalLoaves);
        Assert.Equal([4, 1, 2], plan.Rounds.Select(r => r.Loaves));
        Assert.Equal([(white, 3), (rye, 1)], plan.Rounds[0].Items.Select(i => (i.RecipeId, i.Loaves)));
        Assert.Equal(rye, Assert.Single(plan.Rounds[1].Items).RecipeId);
        Assert.Equal([0, 45, 90], plan.Rounds.Select(r => r.StartOffsetMinutes));
        Assert.Equal(120, plan.TotalOvenMinutes);
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("with, comma", "\"with, comma\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("=SUM(A1)", "'=SUM(A1)")]
    public void US50_CsvCells_AreQuotedAndFormulaSafe(string value, string expected) => Assert.Equal(expected, Csv.Format(value));

    [Fact]
    public void US50_CsvNumbersAndDates_AreInvariant()
    {
        Assert.Equal("11.22", Csv.Format(11.22m));
        Assert.Equal("2026-11-15", Csv.Format(new DateOnly(2026, 11, 15)));
        Assert.Equal(string.Empty, Csv.Format(null));
    }
}
