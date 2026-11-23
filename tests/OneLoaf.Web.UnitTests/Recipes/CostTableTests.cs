using System.Text.Json;
using System.Text.Json.Nodes;
using OneLoaf.Web.Features.Recipes.Components;
using OneLoaf.Web.Shared.Api;
using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.UnitTests.Recipes;

public sealed class CostTableTests : BunitContext
{
    [Fact]
    public void Render_RecordedCost_ShowsEveryLineInOrderAndTheApiTotal()
    {
        var cost = RecordedCost();

        var cut = Render<CostTable>(p => p.Add(t => t.Cost, cost));

        var rows = cut.FindAll("tbody tr");
        Assert.Equal(cost.Lines.Select(l => l.Description), rows.Select(r => r.QuerySelector("td")!.TextContent));
        Assert.Contains(rows, r => r.TextContent.Contains("Electricity", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.TextContent.Contains("Packaging", StringComparison.Ordinal));
        Assert.Contains(rows, r => r.TextContent.Contains("Loss", StringComparison.Ordinal));
        var total = Assert.Single(cut.FindAll("tr.is-total"));
        var totalLine = cost.Lines.Single(l => l.Type == CostLineTypes.Total);
        Assert.Contains(totalLine.Amount!.Value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " EUR", total.TextContent, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll(".ol-alert"));
        Assert.Equal("Amount", rows[0].QuerySelectorAll("td")[3].GetAttribute("data-label"));
    }

    [Fact]
    public void Render_IncompleteCost_ListsMissingPricesAndMarksTheLine()
    {
        var flour = Guid.NewGuid();
        var cost = new CostBreakdownDto(Guid.NewGuid(), 2, new DateOnly(2026, 11, 23), "EUR", false,
            [
                new CostLineDto(1, "ingredient", flour, "Rye flour", 600m, "g", null, null, false),
                new CostLineDto(2, "total", null, "Total", 2m, "loaves", null, null, false),
            ],
            null,
            [new MissingPriceDto(flour, "Rye flour")]);

        var cut = Render<CostTable>(p => p.Add(t => t.Cost, cost));

        Assert.Contains("No price on 23 Nov 2026 for: Rye flour.", cut.Find(".ol-alert").TextContent);
        Assert.Contains("price missing", cut.Find("tr.is-missing").TextContent);
        Assert.Equal("ingredients", cut.Find(".ol-alert a").GetAttribute("href"));
    }

    private static CostBreakdownDto RecordedCost()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(dir!.FullName, "OneLoaf.slnx")))
        {
            dir = dir.Parent;
        }

        var cases = JsonNode.Parse(File.ReadAllText(Path.Combine(dir.FullName, "docs", "api", "examples", "core-api", "GetRecipeCost.json")))!;
        var body = cases.AsArray().First(c => (string?)c!["case"] == "typical")!["responseBody"]!;
        return body.Deserialize<CostBreakdownDto>(new ApiJsonOptions([SharedJsonContext.Default]).Options)!;
    }
}
