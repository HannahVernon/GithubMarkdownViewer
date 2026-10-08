using System.Linq;
using Avalonia;
using GithubMarkdownViewer.Preview;
using GithubMarkdownViewer.Services;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class SelectionTests
{
    private static LayoutResult Layout(string markdown, double width = 400)
    {
        var nodes = DocumentBuilder.Build(markdown, new MarkdownService().Pipeline);
        return new LayoutEngine(new FakeTextProvider()).Layout(nodes, width);
    }

    private static string AllText(LayoutResult layout)
    {
        var all = DocumentSelection.SelectAll(layout)!.Value;
        return DocumentSelection.ExtractText(layout, all.Start, all.End);
    }

    // ── Positions ─────────────────────────────────────────────────

    [Fact]
    public void DocPositionsOrderByBoxThenOffset()
    {
        Assert.True(new DocPosition(0, 9) < new DocPosition(1, 0));
        Assert.True(new DocPosition(2, 3) > new DocPosition(2, 1));
        Assert.True(new DocPosition(1, 1) <= new DocPosition(1, 1));
    }

    [Fact]
    public void PointInsideTextMapsToTheCharacterOffset()
    {
        using var layout = Layout("one\n\ntwo");

        // 16 px is two characters in.
        Assert.Equal(new DocPosition(0, 2), DocumentSelection.PositionAt(layout, new Point(16, 8)));
        Assert.Equal(new DocPosition(1, 1), DocumentSelection.PositionAt(layout, new Point(8, 36)));
    }

    [Fact]
    public void PointInTheGapBetweenBlocksSnapsToTheNearerBlock()
    {
        using var layout = Layout("one\n\ntwo");

        // The first block ends at 16 and the second starts at 28.
        Assert.Equal(0, DocumentSelection.PositionAt(layout, new Point(0, 18))!.Value.Box);
        Assert.Equal(1, DocumentSelection.PositionAt(layout, new Point(0, 24))!.Value.Box);
    }

    [Fact]
    public void PointOutsideTheDocumentSnapsToItsStartOrEnd()
    {
        using var layout = Layout("one\n\ntwo");

        Assert.Equal(new DocPosition(0, 0), DocumentSelection.PositionAt(layout, new Point(0, -50)));
        Assert.Equal(new DocPosition(1, 3), DocumentSelection.PositionAt(layout, new Point(1000, 999)));
    }

    [Fact]
    public void EmptyDocumentHasNoPosition()
    {
        using var layout = Layout("");

        Assert.Null(DocumentSelection.PositionAt(layout, new Point(5, 5)));
        Assert.Null(DocumentSelection.SelectAll(layout));
    }

    [Fact]
    public void PointInATableRowPicksTheCellUnderIt()
    {
        using var layout = Layout("| a | b |\n|---|---|\n| 1 | 2 |");

        foreach (var box in layout.TextBoxes)
        {
            var centre = new Point(box.Bounds.X + 2, box.Bounds.Y + 4);
            Assert.Equal(box.Index, DocumentSelection.PositionAt(layout, centre)!.Value.Box);
        }
    }

    [Fact]
    public void CodeBlockScrollShiftsTheHitOffset()
    {
        var line = new string('x', 40);
        using var layout = Layout($"```\n{line}\n```", 160);
        var point = new Point(16 + 32, 24);

        Assert.Equal(4, DocumentSelection.PositionAt(layout, point, _ => 0)!.Value.Offset);
        Assert.Equal(6, DocumentSelection.PositionAt(layout, point, _ => 16)!.Value.Offset);
    }

    [Fact]
    public void PointInTheCodeBlockPaddingStillSelectsTheCode()
    {
        var line = new string('x', 10);
        using var layout = Layout($"intro\n\n```\n{line}\n```", 300);
        var code = layout.Boxes.OfType<CodeBlockBox>().Single();

        var inPadding = new Point(code.Bounds.X + 4, code.Bounds.Y + 4);
        Assert.Equal(code.Content.Index, DocumentSelection.PositionAt(layout, inPadding)!.Value.Box);
    }

    // ── Words ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("hello world", 2, 0, 5)]
    [InlineData("hello world", 5, 5, 6)]
    [InlineData("hello world", 8, 6, 11)]
    [InlineData("hello world", 11, 6, 11)]
    [InlineData("a,b", 1, 1, 2)]
    [InlineData("snake_case id", 3, 0, 10)]
    [InlineData("a   b", 2, 1, 4)]
    [InlineData("", 0, 0, 0)]
    public void WordAtFindsTheRunAroundAnIndex(string text, int index, int start, int end)
    {
        Assert.Equal((start, end), DocumentSelection.WordAt(text, index));
    }

    [Fact]
    public void WordAndBlockSelectionStayInsideOneBox()
    {
        using var layout = Layout("one two\n\nthree");

        var word = DocumentSelection.WordAt(layout, new DocPosition(0, 5));
        Assert.Equal((new DocPosition(0, 4), new DocPosition(0, 7)), word);

        var block = DocumentSelection.BlockAt(layout, new DocPosition(1, 2));
        Assert.Equal((new DocPosition(1, 0), new DocPosition(1, 5)), block);
    }

    // ── Ranges ────────────────────────────────────────────────────

    [Fact]
    public void RangeInCoversPartialAndWholeBoxes()
    {
        var start = new DocPosition(1, 2);
        var end = new DocPosition(3, 4);

        Assert.Null(DocumentSelection.RangeIn(0, 10, start, end));
        Assert.Equal((2, 10), DocumentSelection.RangeIn(1, 10, start, end));
        Assert.Equal((0, 7), DocumentSelection.RangeIn(2, 7, start, end));
        Assert.Equal((0, 4), DocumentSelection.RangeIn(3, 10, start, end));
        Assert.Null(DocumentSelection.RangeIn(4, 10, start, end));
        Assert.Null(DocumentSelection.RangeIn(1, 10, start, start));
    }

    // ── Copied text ───────────────────────────────────────────────

    [Fact]
    public void SelectingEverythingCopiesBlocksSeparatedByBlankLines()
    {
        using var layout = Layout("one\n\ntwo");

        Assert.Equal("one\n\ntwo", AllText(layout));
    }

    [Fact]
    public void PartialSelectionAcrossBlocksKeepsOnlyTheSelectedCharacters()
    {
        using var layout = Layout("one\n\ntwo");

        Assert.Equal("ne\n\ntw", DocumentSelection.ExtractText(layout, new DocPosition(0, 1), new DocPosition(1, 2)));
        Assert.Equal("ne", DocumentSelection.ExtractText(layout, new DocPosition(0, 1), new DocPosition(0, 3)));
    }

    [Fact]
    public void EmptyOrReversedSelectionCopiesNothing()
    {
        using var layout = Layout("one\n\ntwo");

        Assert.Equal("", DocumentSelection.ExtractText(layout, new DocPosition(0, 1), new DocPosition(0, 1)));
        Assert.Equal("", DocumentSelection.ExtractText(layout, new DocPosition(1, 0), new DocPosition(0, 1)));
    }

    [Fact]
    public void SelectionTouchingOnlyTheEdgeOfABlockDoesNotCopyItsSeparator()
    {
        using var layout = Layout("one\n\ntwo");

        // Starts at the very end of "one" and ends inside "two".
        Assert.Equal("two", DocumentSelection.ExtractText(layout, new DocPosition(0, 3), new DocPosition(1, 3)));
        // Starts in "one" and ends at the very start of "two".
        Assert.Equal("one", DocumentSelection.ExtractText(layout, new DocPosition(0, 0), new DocPosition(1, 0)));
    }

    [Fact]
    public void ListItemsAreCopiedOnePerLineWithoutBullets()
    {
        using var layout = Layout("- one\n- two\n- three");

        Assert.Equal("one\ntwo\nthree", AllText(layout));
    }

    [Fact]
    public void AListAfterAParagraphStartsAfterABlankLine()
    {
        using var layout = Layout("intro\n\n- one\n- two\n\noutro");

        Assert.Equal("intro\n\none\ntwo\n\noutro", AllText(layout));
    }

    [Fact]
    public void NestedListItemsAreCopiedOnSeparateLines()
    {
        using var layout = Layout("- a\n  - b\n  - c\n- d");

        Assert.Equal("a\nb\nc\nd", AllText(layout));
    }

    [Fact]
    public void ListAfterAQuoteStartsAfterABlankLine()
    {
        using var layout = Layout("> quoted\n\n1. first\n2. second");

        Assert.Equal("quoted\n\nfirst\nsecond", AllText(layout));
    }

    [Fact]
    public void TableCellsAreCopiedTabSeparatedByRow()
    {
        using var layout = Layout("| a | b |\n|---|---|\n| 1 | 2 |");

        Assert.Equal("a\tb\n1\t2", AllText(layout));
    }

    [Fact]
    public void TableAfterAParagraphStartsAfterABlankLine()
    {
        using var layout = Layout("intro\n\n| a | b |\n|---|---|\n| 1 | 2 |");

        Assert.Equal("intro\n\na\tb\n1\t2", AllText(layout));
    }

    [Fact]
    public void EmptyTableCellsKeepTheirTabs()
    {
        using var layout = Layout("| a | b | c |\n|---|---|---|\n| 1 |  | 3 |");

        Assert.Equal("a\tb\tc\n1\t\t3", AllText(layout));
    }

    [Fact]
    public void CodeBlockTextIsCopiedAsWritten()
    {
        using var layout = Layout("text\n\n```\nfirst\n  second\n```");

        Assert.Equal("text\n\nfirst\n  second", AllText(layout));
    }

    [Fact]
    public void LinkTextIsCopiedWithoutTheUrl()
    {
        using var layout = Layout("see [the docs](https://example.com) now");

        Assert.Equal("see the docs now", AllText(layout));
    }

    // ── Links ─────────────────────────────────────────────────────

    [Fact]
    public void LinkAtFindsTheUrlOnlyOverLinkText()
    {
        using var layout = Layout("see [docs](https://x.y/z) now");

        // "see docs now": the link covers characters 4 to 7, which is x 32 to 64.
        Assert.Equal("https://x.y/z", DocumentSelection.LinkAt(layout, new Point(40, 8)));
        Assert.Null(DocumentSelection.LinkAt(layout, new Point(8, 8)));
        Assert.Null(DocumentSelection.LinkAt(layout, new Point(80, 8)));
        Assert.Null(DocumentSelection.LinkAt(layout, new Point(40, 200)));
    }
}
