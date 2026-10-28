using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GithubMarkdownViewer.Preview;
using GithubMarkdownViewer.Services;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class TeamsHtmlTests
{
    private const string Gap = "<p>&nbsp;</p>";

    private static System.Collections.Generic.IReadOnlyList<DocNode> Nodes(string markdown) =>
        DocumentBuilder.Build(markdown, new MarkdownService().Pipeline);

    private static string[] Lines(string html) => html.TrimEnd('\n').Split('\n');

    /// <summary>The visible text of an HTML fragment, for comparing with the text of a layout box.</summary>
    private static string VisibleText(string html) =>
        WebUtility.HtmlDecode(Regex.Replace(html.Replace("<br>", "\n"), "<[^>]+>", "")).Trim('\n');

    [Fact]
    public void BlocksAreSeparatedByAnEmptyParagraph()
    {
        var html = TeamsHtml.Write(Nodes("one\n\ntwo\n\n```\ncode\n```"));

        Assert.Equal(new[] { "<p>one</p>", Gap, "<p>two</p>", Gap, "<pre><code>code</code></pre>" }, Lines(html));
    }

    [Fact]
    public void TheTitleIsSentAsLevelTwoWithAGapAfterIt()
    {
        var html = TeamsHtml.Write(Nodes("# Title\n\ntext"));

        Assert.Equal(new[] { "<h2>Title</h2>", Gap, "<p>text</p>" }, Lines(html));
    }

    [Fact]
    public void OtherHeadingsSitOnTheirSectionWithAGapBefore()
    {
        var html = TeamsHtml.Write(Nodes("intro\n\n## Part\n\ntext\n\n### Sub\n\nmore\n\n#### Deep\n\nend"));

        Assert.Equal(new[]
        {
            "<p>intro</p>", Gap, "<h2>Part</h2>", "<p>text</p>", Gap, "<h3>Sub</h3>", "<p>more</p>", Gap,
            "<p><strong>Deep</strong></p>", "<p>end</p>",
        }, Lines(html));
    }

    [Fact]
    public void ListsHaveNoGapBeforeOrAfter()
    {
        var html = TeamsHtml.Write(Nodes("Before:\n\n1. one\n2. two\n\nAfter"));

        Assert.Equal(new[] { "<p>Before:</p>", "<ol><li>one</li><li>two</li></ol>", "<p>After</p>" }, Lines(html));
    }

    [Fact]
    public void InlineStylesBecomeTags()
    {
        var html = TeamsHtml.Write(Nodes("a **b** *c* ~~d~~ `e` [f](https://x.y/z)"));

        Assert.Equal("<p>a <strong>b</strong> <em>c</em> <s>d</s> <code>e</code> <a href=\"https://x.y/z\">f</a></p>\n", html);
    }

    [Theory]
    [InlineData("[f](other.md)")]
    [InlineData("[f](#section)")]
    [InlineData("[f](file:///C:/secret.txt)")]
    public void LinksToThisComputerBecomePlainText(string markdown)
    {
        Assert.Equal("<p>f</p>\n", TeamsHtml.Write(Nodes(markdown)));
    }

    [Fact]
    public void MailLinksAreKept()
    {
        Assert.Contains("<a href=\"mailto:a@example.com\">", TeamsHtml.Write(Nodes("[mail](mailto:a@example.com)")));
    }

    [Fact]
    public void TextIsEscaped()
    {
        var html = TeamsHtml.Write(Nodes("a &lt; b & `<tag>`\n\n```\nif (a < b && c > d) {}\n```"));

        Assert.Contains("<p>a &lt; b &amp; <code>&lt;tag&gt;</code></p>", html);
        Assert.Contains("<pre><code>if (a &lt; b &amp;&amp; c &gt; d) {}</code></pre>", html);
    }

    [Fact]
    public void TaskItemsShowABox()
    {
        var html = TeamsHtml.Write(Nodes("- [ ] open\n- [x] done"));

        Assert.Equal("<ul><li>\u2610 open</li><li>\u2611 done</li></ul>\n", html);
    }

    [Fact]
    public void TablesKeepTheirHeaderRow()
    {
        var html = TeamsHtml.Write(Nodes("| a | b |\n|---|--:|\n| 1 | `2` |"));

        Assert.Equal("<table><thead><tr><th>a</th><th style=\"text-align:right\">b</th></tr></thead>"
            + "<tbody><tr><td>1</td><td style=\"text-align:right\"><code>2</code></td></tr></tbody></table>\n", html);
    }

    [Fact]
    public void PartOfAParagraphKeepsItsStyles()
    {
        var nodes = Nodes("plain **bold text** end");

        // "plain " is 6 characters; select from the middle of "bold" to the middle of "text".
        var html = TeamsHtml.Write(nodes, new DocPosition(0, 8), new DocPosition(0, 14));

        Assert.Equal("<p><strong>ld tex</strong></p>\n", html);
    }

    [Fact]
    public void ASelectionInsideOneCellIsText()
    {
        var nodes = Nodes("| a | b |\n|---|---|\n| one | two |");

        // Boxes: a, b, one, two.
        var html = TeamsHtml.Write(nodes, new DocPosition(3, 0), new DocPosition(3, 3));

        Assert.Equal("<p>two</p>\n", html);
    }

    [Fact]
    public void ASelectionAcrossCellsKeepsOnlyTheRowsItTouches()
    {
        var nodes = Nodes("| a | b |\n|---|---|\n| 1 | 2 |\n| 3 | 4 |");

        // Boxes: a, b, 1, 2, 3, 4.  Select from "2" to "3".
        var html = TeamsHtml.Write(nodes, new DocPosition(3, 0), new DocPosition(4, 1));

        Assert.Equal("<table><tbody><tr><td></td><td>2</td></tr><tr><td>3</td><td></td></tr></tbody></table>\n", html);
    }

    [Fact]
    public void APartialOrderedListKeepsItsNumbers()
    {
        var nodes = Nodes("1. one\n2. two\n3. three");

        var html = TeamsHtml.Write(nodes, new DocPosition(1, 0), new DocPosition(2, 5));

        Assert.Equal("<ol start=\"2\"><li>two</li><li>three</li></ol>\n", html);
    }

    [Fact]
    public void ASelectionTouchingOnlyTheEdgeOfABlockLeavesItOut()
    {
        var nodes = Nodes("one\n\ntwo\n\nthree");

        // From the end of "one" to the start of "three": only "two".
        var html = TeamsHtml.Write(nodes, new DocPosition(0, 3), new DocPosition(2, 0));

        Assert.Equal("<p>two</p>\n", html);
    }

    [Fact]
    public void ARuleIsIncludedOnlyBetweenSelectedText()
    {
        var nodes = Nodes("one\n\n---\n\ntwo");

        Assert.Contains("<hr>", TeamsHtml.Write(nodes, new DocPosition(0, 0), new DocPosition(1, 3)));
        Assert.DoesNotContain("<hr>", TeamsHtml.Write(nodes, new DocPosition(0, 0), new DocPosition(0, 3)));
        Assert.DoesNotContain("<hr>", TeamsHtml.Write(nodes, new DocPosition(1, 0), new DocPosition(1, 3)));
    }

    [Fact]
    public void EveryLayoutBoxMapsToTheSameTextInTheWriter()
    {
        // One of every block kind, including the ones that are easy to get out of step: list markers
        // (no box), a quote, a nested list, a rule, a missing table cell, an empty cell and raw HTML.
        const string markdown = "# Title\n\nPara with **bold**.\n\n> quoted\n> text\n\n- item\n  - nested\n- [x] task\n\n"
            + "1. first\n\n   second paragraph\n\n---\n\n| h1 | h2 | h3 |\n|---|---|---|\n| a | | c |\n| d |\n\n"
            + "```\ncode line\n```\n\n<div>raw</div>\n\n#### Small\n\nlast";
        var nodes = Nodes(markdown);
        using var layout = new LayoutEngine(new FakeTextProvider()).Layout(nodes, 600);

        Assert.True(layout.TextBoxes.Count > 15);
        foreach (var box in layout.TextBoxes)
        {
            var text = box.Source.Text;
            if (text.Length == 0) continue;

            var html = TeamsHtml.Write(nodes, new DocPosition(box.Index, 0), new DocPosition(box.Index, text.Length));

            Assert.Equal(text.TrimEnd('\n'), VisibleText(html).TrimStart('\u2610', '\u2611', ' '));
        }
    }

    [Fact]
    public void CfHtmlOffsetsPointAtTheFragment()
    {
        const string fragment = "<p>caf\u00e9 \u2713 <strong>bold</strong></p>";

        var bytes = HtmlClipboard.ToCfHtml(fragment);
        var all = Encoding.UTF8.GetString(bytes);

        int Offset(string name) => int.Parse(Regex.Match(all, name + @":(\d+)").Groups[1].Value);
        var start = Offset("StartFragment");
        var end = Offset("EndFragment");
        Assert.Equal(fragment, Encoding.UTF8.GetString(bytes, start, end - start));
        Assert.Equal(bytes.Length, Offset("EndHTML"));
        Assert.StartsWith("<html>", Encoding.UTF8.GetString(bytes, Offset("StartHTML"), 6));
    }
}
