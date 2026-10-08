using System.Linq;
using Avalonia;
using GithubMarkdownViewer.Preview;
using GithubMarkdownViewer.Services;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class AutomationModelTests
{
    private static LayoutResult Layout(string markdown, double width = 400)
    {
        var nodes = DocumentBuilder.Build(markdown, new MarkdownService().Pipeline);
        return new LayoutEngine(new FakeTextProvider()).Layout(nodes, width);
    }

    private static System.Collections.Generic.IReadOnlyList<BlockInfo> Blocks(string markdown, double width = 400)
    {
        using var layout = Layout(markdown, width);
        return AutomationModel.Build(layout);
    }

    [Fact]
    public void BlocksComeInReadingOrderWithTheirText()
    {
        var blocks = Blocks("# Title\n\nfirst\n\nsecond");

        Assert.Equal(new[] { "Title", "first", "second" }, blocks.Select(b => b.Name).ToArray());
        Assert.True(blocks[0].Bounds.Y < blocks[1].Bounds.Y);
        Assert.True(blocks[1].Bounds.Y < blocks[2].Bounds.Y);
    }

    [Fact]
    public void HeadingsReportTheirLevel()
    {
        var blocks = Blocks("# One\n\n## Two\n\n#### Four\n\nbody");

        Assert.Equal(new[] { 1, 2, 4, 0 }, blocks.Select(b => b.HeadingLevel).ToArray());
        Assert.Equal(new[] { BlockKind.Heading, BlockKind.Heading, BlockKind.Heading, BlockKind.Text },
            blocks.Select(b => b.Kind).ToArray());
    }

    [Fact]
    public void ListMarkersAreNotReadAsSeparateBlocks()
    {
        var blocks = Blocks("- one\n- two\n\n1. first");

        Assert.Equal(new[] { "one", "two", "first" }, blocks.Select(b => b.Name).ToArray());
    }

    [Fact]
    public void CodeBlocksReportTheWholeBlockAsTheirBounds()
    {
        var blocks = Blocks("```\nfirst\n```", 300);

        var code = Assert.Single(blocks);
        Assert.Equal(BlockKind.Code, code.Kind);
        Assert.Equal(new Rect(0, 0, 300, 16 + 2 * CodeBlockBox.Padding), code.Bounds);
        Assert.Empty(code.Links);
    }

    [Fact]
    public void TableCellsAreBlocksAndEmptyOnesAreSkipped()
    {
        var blocks = Blocks("| a |  |\n|---|---|\n| 1 | 2 |");

        Assert.Equal(new[] { "a", "1", "2" }, blocks.Select(b => b.Name).ToArray());
        Assert.All(blocks, b => Assert.Equal(BlockKind.TableCell, b.Kind));
        // The skipped empty header cell keeps its slot, so indexes still match the selection model.
        Assert.Equal(new[] { 0, 2, 3 }, blocks.Select(b => b.Index).ToArray());
    }

    [Fact]
    public void LinksReportTextUrlAndBoundsInsideTheBlock()
    {
        var block = Assert.Single(Blocks("see [docs](https://x.y/z) now"));

        var link = Assert.Single(block.Links);
        Assert.Equal("docs", link.Text);
        Assert.Equal("https://x.y/z", link.Url);
        // "see docs now": the link covers characters 4 to 7, which is x 32 to 64.
        Assert.Equal(new Rect(32, 0, 32, 16), link.Bounds);
        Assert.True(block.Bounds.Contains(link.Bounds.TopLeft));
    }

    [Fact]
    public void StyledTextInsideALinkIsOneLink()
    {
        var block = Assert.Single(Blocks("[**bold** link](https://x.y)"));

        var link = Assert.Single(block.Links);
        Assert.Equal("bold link", link.Text);
    }

    [Fact]
    public void SeparateLinksStaySeparate()
    {
        var block = Assert.Single(Blocks("[one](https://a.b) and [two](https://c.d)"));

        Assert.Equal(new[] { "one", "two" }, block.Links.Select(l => l.Text).ToArray());
        Assert.Equal(new[] { "https://a.b", "https://c.d" }, block.Links.Select(l => l.Url).ToArray());
        Assert.True(block.Links[0].Bounds.X < block.Links[1].Bounds.X);
    }

    [Fact]
    public void WrappedLinksCoverEveryLine()
    {
        // 80 px is 10 characters, so the link wraps onto a second line.
        var block = Assert.Single(Blocks("[alpha beta gamma](https://x.y)", 80));

        var link = Assert.Single(block.Links);
        Assert.True(link.Bounds.Height > FakeTextProvider.LineHeight);
    }

    [Fact]
    public void EmptyDocumentHasNoBlocks()
    {
        Assert.Empty(Blocks(""));
    }
}
