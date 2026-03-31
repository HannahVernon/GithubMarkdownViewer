using Markdig;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Provides GitHub Flavored Markdown conversion using Markdig.
/// Uses explicit extensions instead of UseAdvancedExtensions() to avoid
/// enabling GenericAttributes (which allows arbitrary HTML attribute injection).
/// </summary>
public class MarkdownService
{
    private readonly MarkdownPipeline _pipeline;

    /// <summary>
    /// Pipeline with DisableHtml for safe rendering (preview and HTML export).
    /// Cached as static to avoid rebuilding on every call.
    /// </summary>
    private static readonly MarkdownPipeline SafePipeline = BuildPipeline(disableHtml: true);

    public MarkdownService()
    {
        _pipeline = BuildPipeline(disableHtml: false);
    }

    /// <summary>
    /// Pipeline for the Avalonia renderer (HTML nodes are manually handled as plain text).
    /// </summary>
    public MarkdownPipeline Pipeline => _pipeline;

    /// <summary>
    /// Pipeline with DisableHtml enabled — use for HTML export and preview rendering.
    /// </summary>
    public MarkdownPipeline SafeExportPipeline => SafePipeline;

    /// <summary>
    /// Converts markdown text to an HTML fragment.
    /// Raw HTML blocks and inlines are escaped to prevent XSS.
    /// </summary>
    public string ToHtml(string markdown)
    {
        return Markdig.Markdown.ToHtml(markdown, SafePipeline);
    }

    /// <summary>
    /// Converts markdown to a full standalone HTML document with GitHub-like styling.
    /// </summary>
    public string ToFullHtml(string markdown)
    {
        var bodyHtml = ToHtml(markdown);
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1" />
                <title>Markdown Export</title>
                <style>
                    body {
                        font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Noto Sans', Helvetica, Arial, sans-serif;
                        font-size: 16px;
                        line-height: 1.5;
                        color: #1f2328;
                        background-color: #ffffff;
                        max-width: 980px;
                        margin: 0 auto;
                        padding: 45px;
                    }
                    h1, h2, h3, h4, h5, h6 { margin-top: 24px; margin-bottom: 16px; font-weight: 600; line-height: 1.25; }
                    h1 { font-size: 2em; padding-bottom: .3em; border-bottom: 1px solid #d1d9e0; }
                    h2 { font-size: 1.5em; padding-bottom: .3em; border-bottom: 1px solid #d1d9e0; }
                    h3 { font-size: 1.25em; }
                    code { padding: .2em .4em; font-size: 85%; background-color: #eff1f3; border-radius: 6px; font-family: ui-monospace, SFMono-Regular, 'SF Mono', Menlo, Consolas, 'Liberation Mono', monospace; }
                    pre { padding: 16px; overflow: auto; font-size: 85%; line-height: 1.45; background-color: #f6f8fa; border-radius: 6px; }
                    pre code { padding: 0; background-color: transparent; }
                    blockquote { padding: 0 1em; color: #656d76; border-left: .25em solid #d1d9e0; margin: 0 0 16px 0; }
                    table { border-spacing: 0; border-collapse: collapse; margin-bottom: 16px; }
                    th, td { padding: 6px 13px; border: 1px solid #d1d9e0; }
                    tr:nth-child(2n) { background-color: #f6f8fa; }
                    th { font-weight: 600; background-color: #f6f8fa; }
                    hr { height: .25em; padding: 0; margin: 24px 0; background-color: #d1d9e0; border: 0; }
                    a { color: #0969da; text-decoration: none; }
                    a:hover { text-decoration: underline; }
                    img { max-width: 100%; }
                    ul, ol { padding-left: 2em; }
                    li + li { margin-top: .25em; }
                    .task-list-item { list-style-type: none; }
                    .task-list-item input { margin: 0 .2em .25em -1.6em; vertical-align: middle; }
                    del { text-decoration: line-through; }
                </style>
            </head>
            <body>
            {{bodyHtml}}
            </body>
            </html>
            """;
    }

    private static MarkdownPipeline BuildPipeline(bool disableHtml)
    {
        var builder = new MarkdownPipelineBuilder()
            .UseEmojiAndSmiley()
            .UseAutoLinks()
            .UseTaskLists()
            .UsePipeTables()
            .UseGridTables()
            .UseFootnotes()
            .UseAutoIdentifiers()
            .UseDefinitionLists()
            .UseAbbreviations()
            .UseFigures()
            .UseMathematics()
            .UseDiagrams()
            .UseEmphasisExtras()
            .UseCitations()
            .UseCustomContainers()
            .UseListExtras();

        if (disableHtml)
            builder.DisableHtml();

        return builder.Build();
    }
}
