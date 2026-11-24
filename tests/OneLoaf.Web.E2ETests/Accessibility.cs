using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace OneLoaf.Web.E2ETests;

/// <summary>axe-core check of the open page (WCAG 2.1 A/AA rules): no serious or critical violation (testing.md §4).</summary>
public static class Accessibility
{
    private static readonly AxeRunOptions Options = new()
    {
        RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa"] },
    };

    /// <summary>Runs axe on the page and fails with the rule ids and targets of serious and critical violations.</summary>
    public static async Task AssertNoSeriousViolationsAsync(IPage page, string screen)
    {
        ArgumentNullException.ThrowIfNull(page);
        var result = await page.RunAxe(Options);
        var serious = result.Violations
            .Where(v => v.Impact is "serious" or "critical")
            .Select(v => $"{v.Id} ({v.Impact}): {string.Join(", ", v.Nodes.Take(3).Select(n => n.Target?.ToString()))}")
            .ToList();
        Assert.True(result.Passes.Length > 0, $"{screen}: axe ran no rules.");
        Assert.True(serious.Count == 0, $"{screen}: {string.Join("; ", serious)}");
    }
}
