using Microsoft.AspNetCore.Components;
using OneLoaf.Web.Shared.Ui;

namespace OneLoaf.Web.UnitTests.Ui;

public sealed class OlConfirmDialogTests : BunitContext
{
    public OlConfirmDialogTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    [Fact]
    public void Buttons_ConfirmAndKeep_RaiseTheirEvents()
    {
        var confirmed = 0;
        var closed = 0;
        var cut = Render<OlConfirmDialog>(p => p
            .Add(x => x.Open, true)
            .Add(x => x.Title, "Delete this price?")
            .Add(x => x.ConfirmText, "Delete price")
            .Add(x => x.OnConfirm, EventCallback.Factory.Create(this, () => confirmed++))
            .Add(x => x.OnClose, EventCallback.Factory.Create(this, () => closed++))
            .AddChildContent("<p>The price is removed.</p>"));

        Assert.Contains("The price is removed.", cut.Markup);
        var buttons = cut.FindAll(".ol-dialog button.ol-btn");
        buttons.Single(b => b.TextContent.Contains("Delete price", StringComparison.Ordinal)).Click();
        cut.FindAll(".ol-dialog button.ol-btn").Single(b => b.TextContent.Contains("Keep it", StringComparison.Ordinal)).Click();

        Assert.Equal(1, confirmed);
        Assert.Equal(1, closed);
    }
}
