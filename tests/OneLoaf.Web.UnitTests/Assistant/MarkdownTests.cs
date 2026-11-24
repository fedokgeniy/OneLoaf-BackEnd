using OneLoaf.Web.Features.Assistant.Components;
using OneLoaf.Web.Features.Assistant.Services;
using OneLoaf.Web.Shared.Contracts.Assistant;

namespace OneLoaf.Web.UnitTests.Assistant;

public sealed class MarkdownTests : BunitContext
{
    [Fact]
    public void Parse_ListWithBoldItems_GivesAParagraphAndABulletList()
    {
        var blocks = MarkdownParser.Parse("I can help you:\n- **Find a recipe** by name\n- **Plan a batch**\n\nWhich one?");

        Assert.Equal([MarkdownBlockKind.Paragraph, MarkdownBlockKind.BulletList, MarkdownBlockKind.Paragraph], blocks.Select(b => b.Kind));
        var first = blocks[1].Items![0];
        Assert.Equal("Find a recipe", first[0].Text);
        Assert.True(first[0].Bold);
        Assert.Equal(" by name", first[1].Text);
        Assert.False(first[1].Bold);
    }

    [Fact]
    public void Parse_HeadingNumberedListCodeAndLink_AreRecognised()
    {
        var blocks = MarkdownParser.Parse("## Plan\n1. Mix `500 g` flour\n2. See [the guide](https://example.org/a)\n\n```\nraw *text*\n```");

        Assert.Equal(MarkdownBlockKind.Heading, blocks[0].Kind);
        Assert.Equal(MarkdownBlockKind.NumberedList, blocks[1].Kind);
        Assert.Contains(blocks[1].Items![0], i => i is { Code: true, Text: "500 g" });
        Assert.Contains(blocks[1].Items![1], i => i is { Text: "the guide", Href: "https://example.org/a" });
        Assert.Equal("raw *text*", blocks[2].Inlines[0].Text);
    }

    [Theory]
    [InlineData("2 * 3 = 6 and snake_case stay")]
    [InlineData("[click](javascript:alert(1))")]
    public void ParseInline_NotMarkdown_StaysPlainText(string text)
    {
        var inline = Assert.Single(MarkdownParser.ParseInline(text));

        Assert.Equal(text, inline.Text);
        Assert.False(inline.Bold || inline.Italic || inline.Code);
        Assert.Null(inline.Href);
    }

    [Fact]
    public void MarkdownText_ReplyWithHtml_RendersMarkupAsTextNeverAsElements()
    {
        var cut = Render<MarkdownText>(p => p.Add(x => x.Text, "**Bold** and <img src=x onerror=alert(1)>\n- one\n- two"));

        Assert.Equal("Bold", cut.Find("strong").TextContent);
        Assert.Equal(2, cut.FindAll("ul li").Count);
        Assert.Empty(cut.FindAll("img"));
        Assert.Contains("<img", cut.Find(".markdown__p").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void PlanCard_RecipeProposal_MarksNewIngredientsAndOffersToSaveTheRecipe()
    {
        var saved = false;
        var proposal = new BatchProposal(Guid.NewGuid(), AssistantApi.CreateRecipe, DateTimeOffset.UtcNow.AddMinutes(30), null,
            new RecipeProposalDto("Beginner loaf", "A first sourdough.", 1, 900m,
                [new("Bread flour", "flour", "g", 500m, false, false), new("Water", "water", "g", 350m, false, true)],
                [new("Bake", "bake", 40, 45)]));

        var cut = Render<PlanCard>(p => p.Add(x => x.Proposal, proposal).Add(x => x.OnConfirm, () => saved = true));

        Assert.Contains("Beginner loaf", cut.Find(".ol-plan__title").TextContent);
        Assert.Contains("500 g", cut.Markup, StringComparison.Ordinal);
        Assert.Equal("New ingredient", Assert.Single(cut.FindAll(".ol-badge")).TextContent);
        Assert.Contains("without a price", cut.Markup, StringComparison.Ordinal);

        cut.FindAll("button").Single(b => b.TextContent.Contains("Save recipe", StringComparison.Ordinal)).Click();
        Assert.True(saved);
    }

    [Fact]
    public void PlanCard_ConfirmedRecipe_LinksTheRecipe()
    {
        var recipeId = Guid.NewGuid();
        var proposal = new BatchProposal(Guid.NewGuid(), AssistantApi.CreateRecipe, DateTimeOffset.UtcNow, null,
            new RecipeProposalDto("Beginner loaf", null, 1, null, [], []));

        var cut = Render<PlanCard>(p => p
            .Add(x => x.Proposal, proposal)
            .Add(x => x.Status, OneLoaf.Web.Features.Assistant.State.ProposalStatus.Confirmed)
            .Add(x => x.RecipeId, recipeId));

        Assert.Equal($"recipes/{recipeId}", cut.Find(".plan-card__result a").GetAttribute("href"));
        Assert.Empty(cut.FindAll("button"));
    }
}
