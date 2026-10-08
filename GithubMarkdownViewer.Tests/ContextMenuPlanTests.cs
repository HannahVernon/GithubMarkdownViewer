using System.Linq;
using Avalonia;
using GithubMarkdownViewer.Preview;
using GithubMarkdownViewer.Services;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class ContextMenuPlanTests
{
    private static LayoutResult Layout(string markdown, double width = 400)
    {
        var nodes = DocumentBuilder.Build(markdown, new MarkdownService().Pipeline);
        return new LayoutEngine(new FakeTextProvider()).Layout(nodes, width);
    }

    private static ContextAction[] Actions(System.Collections.Generic.IReadOnlyList<ContextEntry> entries) =>
        entries.Select(e => e.Action).ToArray();

    // "see docs now": the link covers characters 4 to 7, which is x 32 to 64.
    private const string LinkParagraph = "see [docs](https://x.y/z) now";
    private static readonly Point OnLink = new(40, 8);
    private static readonly Point OffLink = new(8, 8);

    [Fact]
    public void ClickingALinkOffersTheLinkEntriesFirst()
    {
        using var layout = Layout(LinkParagraph);

        var entries = ContextMenuPlan.Build(layout, OnLink, hasSelection: false);

        Assert.Equal(new[] { ContextAction.OpenInBrowser, ContextAction.CopyUrl, ContextAction.CopyBlock, ContextAction.SelectAll },
            Actions(entries));
        Assert.Equal("https://x.y/z", entries[0].Text);
        Assert.Equal("https://x.y/z", entries[1].Text);
        Assert.True(entries[0].Enabled);
        Assert.Equal(entries[0].Group, entries[1].Group);
    }

    [Fact]
    public void ClickingNearALinkButNotOnItOffersNoLinkEntries()
    {
        using var layout = Layout(LinkParagraph);

        var entries = ContextMenuPlan.Build(layout, OffLink, hasSelection: false);

        Assert.DoesNotContain(entries, e => e.Action is ContextAction.OpenInBrowser or ContextAction.CopyUrl);
    }

    [Theory]
    [InlineData("https://example.com/page", true)]
    [InlineData("http://example.com", true)]
    [InlineData("HTTPS://EXAMPLE.COM", true)]
    [InlineData("mailto:someone@example.com", false)]
    [InlineData("#section", false)]
    [InlineData("other.md", false)]
    [InlineData("other.md#heading", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///C:/secret.txt", false)]
    [InlineData("ftp://example.com/file", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OnlyWebAddressesCanBeOpenedInTheBrowser(string? url, bool expected)
    {
        Assert.Equal(expected, ContextMenuPlan.IsWebUrl(url));
    }

    [Theory]
    [InlineData("[go](#section)")]
    [InlineData("[go](other.md)")]
    [InlineData("[go](mailto:someone@example.com)")]
    public void OtherLinksCanBeCopiedButNotOpenedInTheBrowser(string markdown)
    {
        using var layout = Layout(markdown);

        // "go" is at x 0 to 16.
        var entries = ContextMenuPlan.Build(layout, new Point(4, 8), hasSelection: false);

        var open = entries.Single(e => e.Action == ContextAction.OpenInBrowser);
        var copy = entries.Single(e => e.Action == ContextAction.CopyUrl);
        Assert.False(open.Enabled);
        Assert.True(copy.Enabled);
    }

    [Fact]
    public void CopyUrlCopiesTheTargetAsWritten()
    {
        using var layout = Layout("[go](other.md#part)");

        var copy = ContextMenuPlan.Build(layout, new Point(4, 8), false).Single(e => e.Action == ContextAction.CopyUrl);

        Assert.Equal("other.md#part", copy.Text);
    }

    [Fact]
    public void ClickingAParagraphOffersCopyParagraph()
    {
        using var layout = Layout("first\n\nsecond");

        var entries = ContextMenuPlan.Build(layout, new Point(8, 8), hasSelection: false);

        var block = entries.Single(e => e.Action == ContextAction.CopyBlock);
        Assert.Equal("Copy paragraph", block.Label);
        Assert.Equal("first", block.Text);
        Assert.Equal(new[] { ContextAction.CopyBlock, ContextAction.SelectAll }, Actions(entries));
    }

    [Fact]
    public void CopyParagraphUsesTheBlockUnderThePointer()
    {
        using var layout = Layout("first\n\nsecond");

        // The second paragraph starts at y 28.
        var block = ContextMenuPlan.Build(layout, new Point(8, 34), false).Single(e => e.Action == ContextAction.CopyBlock);

        Assert.Equal("second", block.Text);
    }

    [Fact]
    public void BlocksAreNamedAfterWhatTheyAre()
    {
        using var layout = Layout("# Title\n\ntext\n\n```\ncode here\n```\n\n| a | b |\n|---|---|\n| 1 | 2 |");

        string Label(int boxIndex)
        {
            var box = layout.TextBoxes[boxIndex];
            var area = box.ScrollOwner?.Bounds ?? box.Bounds;
            var entries = ContextMenuPlan.Build(layout, area.Center, false);
            return entries.Single(e => e.Action == ContextAction.CopyBlock).Label;
        }

        Assert.Equal("Copy heading", Label(0));
        Assert.Equal("Copy paragraph", Label(1));
        Assert.Equal("Copy code block", Label(2));
        Assert.Equal("Copy cell contents", Label(3));
    }

    [Fact]
    public void CopyCodeBlockCopiesTheWholeCode()
    {
        using var layout = Layout("```\nfirst\n  second\n```");
        var code = layout.Boxes.OfType<CodeBlockBox>().Single();

        // The padding around the code still counts as the code block.
        var entries = ContextMenuPlan.Build(layout, new Point(code.Bounds.X + 4, code.Bounds.Y + 4), false);

        Assert.Equal("first\n  second", entries.Single(e => e.Action == ContextAction.CopyBlock).Text);
    }

    [Fact]
    public void ClickingTheEdgeOfATableCellStillFindsTheCell()
    {
        using var layout = Layout("| a | b |\n|---|---|\n| 1 | 2 |");
        var cell = layout.TextBoxes[3];

        // Inside the cell padding, not over the text.
        var point = new Point(cell.Bounds.X - 5, cell.Bounds.Y - 3);
        var entries = ContextMenuPlan.Build(layout, point, false);

        var block = entries.Single(e => e.Action == ContextAction.CopyBlock);
        Assert.Equal("2", block.Text);
        Assert.Equal("Copy cell contents", block.Label);
    }

    [Fact]
    public void EmptyTableCellsOfferNoCopyBlock()
    {
        using var layout = Layout("| a |  |\n|---|---|\n| 1 | 2 |");
        var empty = layout.TextBoxes[1];

        var entries = ContextMenuPlan.Build(layout, empty.Bounds.Center, false);

        Assert.DoesNotContain(entries, e => e.Action == ContextAction.CopyBlock);
    }

    [Fact]
    public void ASelectionAddsCopyBeforeCopyParagraph()
    {
        using var layout = Layout("first");

        var entries = ContextMenuPlan.Build(layout, new Point(8, 8), hasSelection: true);

        Assert.Equal(new[] { ContextAction.CopySelection, ContextAction.CopyBlock, ContextAction.SelectAll }, Actions(entries));
        Assert.Equal("Ctrl+C", entries[0].Gesture);
        Assert.Equal(entries[0].Group, entries[1].Group);
        Assert.Null(entries[0].Text);
    }

    [Fact]
    public void ClickingBlankSpaceOffersOnlySelectAll()
    {
        using var layout = Layout("first");

        var entries = ContextMenuPlan.Build(layout, new Point(300, 500), hasSelection: false);

        Assert.Equal(new[] { ContextAction.SelectAll }, Actions(entries));
        Assert.Equal("Ctrl+A", entries[0].Gesture);
    }

    [Fact]
    public void BlankSpaceWithASelectionOffersCopyAndSelectAll()
    {
        using var layout = Layout("first");

        var entries = ContextMenuPlan.Build(layout, new Point(300, 500), hasSelection: true);

        Assert.Equal(new[] { ContextAction.CopySelection, ContextAction.SelectAll }, Actions(entries));
    }

    [Fact]
    public void AKeyboardRequestHasNoPositionSoOnlySelectionEntriesApply()
    {
        using var layout = Layout(LinkParagraph);

        Assert.Equal(new[] { ContextAction.SelectAll }, Actions(ContextMenuPlan.Build(layout, null, false)));
        Assert.Equal(new[] { ContextAction.CopySelection, ContextAction.SelectAll },
            Actions(ContextMenuPlan.Build(layout, null, true)));
    }

    [Fact]
    public void GroupsNeverGoBackwardsSoDividersAppearOnlyBetweenSections()
    {
        using var layout = Layout(LinkParagraph);

        var groups = ContextMenuPlan.Build(layout, OnLink, hasSelection: true).Select(e => e.Group).ToList();

        Assert.Equal(groups.OrderBy(g => g).ToList(), groups);
        Assert.Equal(3, groups.Distinct().Count());
    }

    [Fact]
    public void BoxAtFindsOnlyABlockThatContainsThePoint()
    {
        using var layout = Layout("one\n\ntwo");

        Assert.Equal(0, DocumentSelection.BoxAt(layout, new Point(8, 8))!.Index);
        Assert.Equal(1, DocumentSelection.BoxAt(layout, new Point(8, 36))!.Index);
        // The gap between the two paragraphs, and far outside.
        Assert.Null(DocumentSelection.BoxAt(layout, new Point(8, 22)));
        Assert.Null(DocumentSelection.BoxAt(layout, new Point(-50, -50)));
    }
}
