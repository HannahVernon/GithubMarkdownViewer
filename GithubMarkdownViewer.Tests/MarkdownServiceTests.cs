using GithubMarkdownViewer.Services;
using Markdig;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Xunit;

namespace GithubMarkdownViewer.Tests;

public class MarkdownServiceTests
{
    [Fact]
    public void Pipeline_ParsesLeadingYamlFrontMatterAsItsOwnBlock()
    {
        var service = new MarkdownService();
        const string markdown = "---\ntitle: Example\n---\n\n# Heading\n";

        var document = Markdown.Parse(markdown, service.Pipeline);

        Assert.IsType<YamlFrontMatterBlock>(document[0]);
        Assert.IsType<HeadingBlock>(document[1]);
    }

    [Fact]
    public void Pipeline_TreatsThematicBreakAfterContentAsRule()
    {
        var service = new MarkdownService();
        const string markdown = "Paragraph\n\n---\n\nMore\n";

        var document = Markdown.Parse(markdown, service.Pipeline);

        Assert.Contains(document, block => block is ThematicBreakBlock);
        Assert.DoesNotContain(document, block => block is YamlFrontMatterBlock);
    }
}
