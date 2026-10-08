using System.Linq;
using GithubMarkdownViewer.Preview;
using GithubMarkdownViewer.Services;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class DocumentBuilderTests
{
    private static System.Collections.Generic.IReadOnlyList<DocNode> Build(string markdown) =>
        DocumentBuilder.Build(markdown, new MarkdownService().Pipeline);

    private static RichText ParagraphText(string markdown) =>
        Assert.IsType<ParagraphNode>(Build(markdown)[0]).Text;

    [Fact]
    public void FrontMatterProducesNoNode()
    {
        var nodes = Build("---\ntitle: x\n---\n\n# Heading\n");

        var heading = Assert.IsType<HeadingNode>(Assert.Single(nodes));
        Assert.Equal(1, heading.Level);
    }

    [Fact]
    public void InlineStylesBecomeSpans()
    {
        var text = ParagraphText("a **bold** *it* `code` ~~gone~~");

        Assert.Equal("a bold it code gone", text.Text);
        Assert.Collection(text.Spans,
            s => { Assert.Equal(SpanFlags.Bold, s.Flags); Assert.Equal("bold", text.Text.Substring(s.Start, s.Length)); },
            s => { Assert.Equal(SpanFlags.Italic, s.Flags); Assert.Equal("it", text.Text.Substring(s.Start, s.Length)); },
            s => { Assert.Equal(SpanFlags.Code, s.Flags); Assert.Equal("code", text.Text.Substring(s.Start, s.Length)); },
            s => { Assert.Equal(SpanFlags.Strike, s.Flags); Assert.Equal("gone", text.Text.Substring(s.Start, s.Length)); });
    }

    [Fact]
    public void NestedEmphasisCombinesFlagsWithoutOverlap()
    {
        var text = ParagraphText("**a *b* c**");

        Assert.Equal("a b c", text.Text);
        Assert.Collection(text.Spans,
            s => { Assert.Equal(SpanFlags.Bold, s.Flags); Assert.Equal(0, s.Start); Assert.Equal(2, s.Length); },
            s => { Assert.Equal(SpanFlags.Bold | SpanFlags.Italic, s.Flags); Assert.Equal(2, s.Start); Assert.Equal(1, s.Length); },
            s => { Assert.Equal(SpanFlags.Bold, s.Flags); Assert.Equal(3, s.Start); Assert.Equal(2, s.Length); });
    }

    [Fact]
    public void LinkTextCarriesItsUrl()
    {
        var text = ParagraphText("see [the docs](https://example.com/docs) now");

        Assert.Equal("see the docs now", text.Text);
        Assert.Null(text.UrlAt(0));
        Assert.Equal("https://example.com/docs", text.UrlAt(5));
        Assert.Equal("https://example.com/docs", text.UrlAt(11));
        Assert.Null(text.UrlAt(13));
    }

    [Fact]
    public void AutolinkUsesItsAddressAsText()
    {
        var text = ParagraphText("visit https://example.com today");

        Assert.Contains("https://example.com", text.Text);
        Assert.Equal("https://example.com", text.UrlAt(text.Text.IndexOf("https", System.StringComparison.Ordinal)));
    }

    [Fact]
    public void LineBreakBecomesNewline()
    {
        var text = ParagraphText("one  \ntwo");

        Assert.Equal("one\ntwo", text.Text);
    }

    [Fact]
    public void TaskListItemsRecordCheckedState()
    {
        var list = Assert.IsType<ListNode>(Build("- [x] done\n- [ ] open\n- plain")[0]);

        Assert.Equal(new bool?[] { true, false, null }, list.Items.Select(i => i.TaskChecked).ToArray());
        Assert.Equal("done", Assert.IsType<ParagraphNode>(list.Items[0].Children[0]).Text.Text);
    }

    [Fact]
    public void OrderedListKeepsItsStartNumber()
    {
        var list = Assert.IsType<ListNode>(Build("3. a\n4. b")[0]);

        Assert.True(list.Ordered);
        Assert.Equal(3, list.Start);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact]
    public void ListsWithBlankLinesBetweenItemsAreNotTight()
    {
        Assert.True(Assert.IsType<ListNode>(Build("- a\n- b")[0]).Tight);
        Assert.False(Assert.IsType<ListNode>(Build("- a\n\n- b")[0]).Tight);
    }

    [Fact]
    public void TableKeepsCellTextHeaderAndAlignment()
    {
        var table = Assert.IsType<TableNode>(Build("| a | b | c |\n|:--|:-:|--:|\n| 1 | 2 | 3 |")[0]);

        Assert.Equal(3, table.Columns);
        Assert.Equal(2, table.Rows.Count);
        Assert.True(table.Rows[0].IsHeader);
        Assert.False(table.Rows[1].IsHeader);
        Assert.Equal(new[] { CellAlign.Left, CellAlign.Center, CellAlign.Right },
            table.Rows[1].Cells.Select(c => c.Align).ToArray());
        Assert.Equal(new[] { "1", "2", "3" }, table.Rows[1].Cells.Select(c => c.Text.Text).ToArray());
    }

    [Fact]
    public void BlocksRecordOneBasedSourceLines()
    {
        var nodes = Build("# Title\n\ntext\nmore\n");

        Assert.Equal((1, 1), (nodes[0].StartLine, nodes[0].EndLine));
        Assert.Equal((3, 4), (nodes[1].StartLine, nodes[1].EndLine));
    }

    [Fact]
    public void ContainerAndFencedBlocksCoverAllTheirSourceLines()
    {
        // Lines: 1-3 list, 4 blank, 5-8 fenced code, 9 blank, 10-11 quote.
        var nodes = Build("- one\n- two\n- three\n\n```\ncode\nmore\n```\n\n> q1\n> q2\n");

        Assert.Equal((1, 3), (nodes[0].StartLine, nodes[0].EndLine));
        Assert.Equal((5, 8), (nodes[1].StartLine, nodes[1].EndLine));
        Assert.Equal((10, 11), (nodes[2].StartLine, nodes[2].EndLine));
    }

    [Fact]
    public void LastBlockWithoutTrailingNewlineStillHasAnEndLine()
    {
        var nodes = Build("a\n\nlast\nline");

        Assert.Equal((3, 4), (nodes[1].StartLine, nodes[1].EndLine));
    }
}
