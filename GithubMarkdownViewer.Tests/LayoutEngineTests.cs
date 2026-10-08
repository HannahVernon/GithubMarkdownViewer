using System.Linq;
using Avalonia;
using GithubMarkdownViewer.Preview;
using GithubMarkdownViewer.Services;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class LayoutEngineTests
{
    private static LayoutResult Layout(string markdown, double width)
    {
        var nodes = DocumentBuilder.Build(markdown, new MarkdownService().Pipeline);
        return new LayoutEngine(new FakeTextProvider()).Layout(nodes, width);
    }

    [Fact]
    public void ParagraphWrapsToTheContentWidth()
    {
        // 80 px is 10 characters, so "aaaa bbbb cccc dddd" wraps into two lines of 16 px.
        using var layout = Layout("aaaa bbbb cccc dddd", 80);

        var box = Assert.IsType<TextLayoutBox>(Assert.Single(layout.Boxes));
        Assert.Equal(32, box.Bounds.Height);
        Assert.Equal(80, box.Bounds.Width);
    }

    [Fact]
    public void ParagraphsAreSeparatedByTheBlockGap()
    {
        using var layout = Layout("one\n\ntwo", 200);

        var boxes = layout.TextBoxes;
        Assert.Equal(0, boxes[0].Bounds.Y);
        Assert.Equal(16 + 12, boxes[1].Bounds.Y);
        Assert.Equal(boxes[1].Bounds.Bottom + 12, layout.Height);
    }

    [Fact]
    public void HeadingsRecordTheirPositionByAnchorId()
    {
        using var layout = Layout("# Title\n\ntext\n\n### Deep Section\n", 400);

        Assert.Equal(16, layout.HeadingPositions["title"]);
        Assert.True(layout.HeadingPositions["deep-section"] > layout.HeadingPositions["title"]);
    }

    [Fact]
    public void TopLevelBlocksProduceOrderedAnchorsWithSourceLines()
    {
        using var layout = Layout("# Title\n\nfirst\n\nsecond\n", 400);

        Assert.Equal(new[] { 1, 3, 5 }, layout.Anchors.Select(a => a.StartLine).ToArray());
        Assert.True(layout.Anchors[0].Y < layout.Anchors[1].Y);
        Assert.True(layout.Anchors[1].Y < layout.Anchors[2].Y);
        Assert.All(layout.Anchors, a => Assert.True(a.Height > 0));
    }

    [Fact]
    public void ListItemsAreIndentedAndMarkersAreNotSelectable()
    {
        using var layout = Layout("- one\n- two", 400);

        // Indent 20 + marker column 16 + gap 6.
        Assert.Equal(new[] { 42.0, 42.0 }, layout.TextBoxes.Select(t => t.Bounds.X).ToArray());
        Assert.Equal(2, layout.TextBoxes.Count);
        Assert.Equal(4, layout.Boxes.OfType<TextLayoutBox>().Count());
        Assert.Equal(2, layout.Boxes.OfType<TextLayoutBox>().Count(t => !t.Selectable));
    }

    [Fact]
    public void TightListItemsHaveNoGapBelowParagraphs()
    {
        using var layout = Layout("- one\n- two", 400);

        // Item height 16 plus the 4 px item spacing.
        Assert.Equal(new[] { 0.0, 20.0 }, layout.TextBoxes.Select(t => t.Bounds.Y).ToArray());
    }

    [Fact]
    public void TaskItemsGetACheckMarkInsteadOfABullet()
    {
        using var layout = Layout("- [x] done\n- [ ] open", 400);

        var marks = layout.Boxes.OfType<CheckMarkBox>().ToList();
        Assert.Equal(new[] { true, false }, marks.Select(m => m.Checked).ToArray());
        Assert.DoesNotContain(layout.Boxes.OfType<TextLayoutBox>(), t => !t.Selectable);
    }

    [Fact]
    public void CodeBlocksDoNotWrapAndScrollSideways()
    {
        var line = new string('x', 40);
        using var layout = Layout($"```\n{line}\n```", 160);

        var code = Assert.IsType<CodeBlockBox>(Assert.Single(layout.Boxes));
        Assert.Equal(16 + 2 * CodeBlockBox.Padding, code.Bounds.Height);
        Assert.Equal(160, code.Bounds.Width);
        Assert.Equal(40 * FakeTextProvider.CharWidth, code.Content.Text.Width);
        Assert.Equal(40 * FakeTextProvider.CharWidth - (160 - 2 * CodeBlockBox.Padding), code.MaxScrollX);
        Assert.Same(code.Content, Assert.Single(layout.TextBoxes));
    }

    [Fact]
    public void ShortCodeDoesNotScroll()
    {
        using var layout = Layout("```\nhi\n```", 300);

        Assert.Equal(0, Assert.IsType<CodeBlockBox>(layout.Boxes[0]).MaxScrollX);
    }

    [Fact]
    public void QuotesIndentTheirContentAndDrawABar()
    {
        using var layout = Layout("> quoted", 300);

        var text = Assert.Single(layout.TextBoxes);
        Assert.Equal(4 + 16, text.Bounds.X);
        var bar = Assert.Single(layout.Boxes.OfType<FillBox>());
        Assert.Equal(PaletteColor.QuoteBorder, bar.Fill);
        Assert.Equal(4, bar.Bounds.Width);
    }

    [Fact]
    public void TableCellsAreLaidOutRowByRow()
    {
        using var layout = Layout("| a | b |\n|---|---|\n| 1 | 2 |", 400);

        Assert.Equal(4, layout.TextBoxes.Count);
        Assert.Equal(new[] { "a", "b", "1", "2" }, layout.TextBoxes.Select(t => t.Source.Text).ToArray());

        var header = layout.TextBoxes.Take(2).ToList();
        var body = layout.TextBoxes.Skip(2).ToList();
        Assert.Equal(header[0].Bounds.Y, header[1].Bounds.Y);
        Assert.True(body[0].Bounds.Y > header[0].Bounds.Y);
        Assert.True(body[1].Bounds.X > body[0].Bounds.X);
    }

    [Fact]
    public void TableCellsWrapWhenTheTableIsNarrow()
    {
        var wide = "word " + string.Join(' ', Enumerable.Repeat("filler", 12));
        using var layout = Layout($"| a | b |\n|---|---|\n| x | {wide} |", 220);

        var last = layout.TextBoxes[^1];
        Assert.True(last.Bounds.Height > FakeTextProvider.LineHeight);
        Assert.True(last.Bounds.Right <= 220);
    }

    [Fact]
    public void RuleIsAThinFilledBox()
    {
        using var layout = Layout("one\n\n---\n\ntwo", 300);

        var rule = Assert.Single(layout.Boxes.OfType<FillBox>());
        Assert.Equal(PaletteColor.HrBackground, rule.Fill);
        Assert.Equal(3, rule.Bounds.Height);
    }

    [Fact]
    public void ColumnWidthsUseNaturalWidthWhenTheyFit()
    {
        var widths = LayoutEngine.ColumnWidths(new[] { 10.0, 10.0 }, new[] { 30.0, 30.0 }, 100);

        Assert.Equal(new[] { 30.0, 30.0 }, widths);
    }

    [Fact]
    public void ColumnWidthsShareExtraSpaceInProportionToGrowth()
    {
        var widths = LayoutEngine.ColumnWidths(new[] { 10.0, 10.0 }, new[] { 20.0, 50.0 }, 50);

        // Extra space is 30; the columns can grow by 10 and 40, so they take 6 and 24.
        Assert.Equal(16, widths[0], 6);
        Assert.Equal(34, widths[1], 6);
    }

    [Fact]
    public void ColumnWidthsShrinkBelowTheLongestWordOnlyWhenNothingElseFits()
    {
        var widths = LayoutEngine.ColumnWidths(new[] { 40.0, 40.0 }, new[] { 60.0, 60.0 }, 40);

        Assert.Equal(new[] { 20.0, 20.0 }, widths);
        Assert.Equal(40, widths.Sum(), 6);
    }

    [Fact]
    public void EmptyDocumentHasZeroHeight()
    {
        using var layout = Layout("", 300);

        Assert.Equal(0, layout.Height);
        Assert.Empty(layout.Boxes);
    }
}
