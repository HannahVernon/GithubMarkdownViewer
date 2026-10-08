using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace GithubMarkdownViewer.Preview;

/// <summary>Turns a Markdig syntax tree into the preview document model.</summary>
public sealed class DocumentBuilder
{
    /// <summary>Character offset where each source line starts, used to turn block spans into line numbers.</summary>
    private readonly int[] _lineStarts;

    private DocumentBuilder(string markdown)
    {
        var starts = new List<int> { 0 };
        for (var i = 0; i < markdown.Length; i++)
        {
            if (markdown[i] == '\n') starts.Add(i + 1);
        }
        _lineStarts = starts.ToArray();
    }

    public static IReadOnlyList<DocNode> Build(string markdown, MarkdownPipeline pipeline)
    {
        var document = Markdown.Parse(markdown, pipeline);
        return new DocumentBuilder(markdown).BuildBlocks(document).ToList();
    }

    private IEnumerable<DocNode> BuildBlocks(ContainerBlock container)
    {
        foreach (var block in container)
        {
            foreach (var node in BuildBlock(block))
                yield return node;
        }
    }

    private IEnumerable<DocNode> BuildBlock(Block block)
    {
        switch (block)
        {
            case YamlFrontMatterBlock:
                // Front matter is metadata and is not shown, matching GitHub.
                yield break;

            case HeadingBlock heading:
                yield return WithLines(new HeadingNode
                {
                    Level = heading.Level,
                    Id = heading.GetAttributes().Id,
                    Text = CollectInlines(heading.Inline),
                }, heading);
                break;

            case ParagraphBlock paragraph:
                yield return WithLines(new ParagraphNode { Text = CollectInlines(paragraph.Inline) }, paragraph);
                break;

            case FencedCodeBlock or CodeBlock:
                yield return WithLines(new CodeNode { Text = ((LeafBlock)block).Lines.ToString().TrimEnd() }, block);
                break;

            case QuoteBlock quote:
            {
                var node = WithLines(new QuoteNode(), quote);
                node.Children.AddRange(BuildBlocks(quote));
                yield return node;
                break;
            }

            case ListBlock list:
                yield return BuildList(list);
                break;

            case ThematicBreakBlock:
                yield return WithLines(new RuleNode(), block);
                break;

            case Table table:
                yield return BuildTable(table);
                break;

            case HtmlBlock html:
            {
                var text = html.Lines.ToString().Trim();
                if (text.Length > 0)
                    yield return WithLines(new HtmlNode { Text = text }, html);
                break;
            }

            default:
                if (block is LeafBlock leaf && leaf.Inline != null)
                {
                    yield return WithLines(new ParagraphNode { Text = CollectInlines(leaf.Inline) }, leaf);
                }
                else if (block is ContainerBlock container)
                {
                    foreach (var child in BuildBlocks(container))
                        yield return child;
                }
                break;
        }
    }

    private ListNode BuildList(ListBlock list)
    {
        var start = 1;
        if (list.IsOrdered && int.TryParse(list.OrderedStart, out var parsed))
            start = parsed;

        var node = WithLines(new ListNode { Ordered = list.IsOrdered, Start = start, Tight = !list.IsLoose }, list);

        foreach (var item in list.OfType<ListItemBlock>())
        {
            var firstParagraph = item.OfType<ParagraphBlock>().FirstOrDefault();
            var task = firstParagraph?.Inline?.FirstChild as TaskList;

            var itemNode = new ListItemNode { TaskChecked = task?.Checked };
            foreach (var child in BuildBlocks(item))
                itemNode.Children.Add(child);

            if (task != null && itemNode.Children.Count > 0 && itemNode.Children[0] is ParagraphNode first)
                itemNode.Children[0] = new ParagraphNode
                {
                    StartLine = first.StartLine,
                    EndLine = first.EndLine,
                    Text = TrimStart(first.Text),
                };

            node.Items.Add(itemNode);
        }

        return node;
    }

    private TableNode BuildTable(Table table)
    {
        var columns = table.ColumnDefinitions?.Count ?? 1;
        var node = WithLines(new TableNode { Columns = Math.Max(1, columns) }, table);

        foreach (var rowObj in table)
        {
            if (rowObj is not TableRow row) continue;

            var rowNode = new TableRowNode { IsHeader = row.IsHeader };
            var columnIndex = 0;
            foreach (var cellObj in row)
            {
                if (cellObj is not TableCell cell) continue;

                var collector = new InlineCollector();
                var first = true;
                foreach (var cellBlock in cell)
                {
                    if (cellBlock is not LeafBlock leaf || leaf.Inline == null) continue;
                    if (!first) collector.Append("\n", SpanFlags.None, null);
                    collector.Visit(leaf.Inline, SpanFlags.None, null);
                    first = false;
                }

                var align = CellAlign.Left;
                if (table.ColumnDefinitions != null && columnIndex < table.ColumnDefinitions.Count)
                {
                    align = table.ColumnDefinitions[columnIndex].Alignment switch
                    {
                        TableColumnAlign.Center => CellAlign.Center,
                        TableColumnAlign.Right => CellAlign.Right,
                        _ => CellAlign.Left,
                    };
                }

                rowNode.Cells.Add(new TableCellNode { Text = collector.ToRichText(), Align = align });
                columnIndex++;
            }

            node.Rows.Add(rowNode);
        }

        return node;
    }

    private static RichText CollectInlines(ContainerInline? container)
    {
        if (container == null) return RichText.Empty;
        var collector = new InlineCollector();
        collector.Visit(container, SpanFlags.None, null);
        return collector.ToRichText();
    }

    private static RichText TrimStart(RichText text)
    {
        var trimmed = text.Text.TrimStart();
        var removed = text.Text.Length - trimmed.Length;
        if (removed == 0) return text;

        var spans = new List<TextSpan>();
        foreach (var span in text.Spans)
        {
            var start = Math.Max(0, span.Start - removed);
            var end = span.End - removed;
            if (end > start)
                spans.Add(span with { Start = start, Length = end - start });
        }
        return new RichText(trimmed, spans);
    }

    private T WithLines<T>(T node, Block block) where T : DocNode
    {
        node.StartLine = block.Line + 1;
        node.EndLine = Math.Max(node.StartLine, LineOf(block.Span.End) + 1);
        return node;
    }

    /// <summary>Returns the 0-based source line that contains a character offset.</summary>
    private int LineOf(int offset)
    {
        var index = Array.BinarySearch(_lineStarts, Math.Max(0, offset));
        return index >= 0 ? index : ~index - 1;
    }

    /// <summary>Flattens nested inlines into one string with non-overlapping style spans.</summary>
    private sealed class InlineCollector
    {
        private readonly StringBuilder _text = new();
        private readonly List<TextSpan> _spans = new();

        public RichText ToRichText() => new(_text.ToString(), _spans.ToList());

        public void Append(string value, SpanFlags flags, string? url)
        {
            if (value.Length == 0) return;

            var start = _text.Length;
            _text.Append(value);

            if (flags == SpanFlags.None && url == null) return;

            if (_spans.Count > 0)
            {
                var last = _spans[^1];
                if (last.End == start && last.Flags == flags && last.Url == url)
                {
                    _spans[^1] = last with { Length = last.Length + value.Length };
                    return;
                }
            }
            _spans.Add(new TextSpan(start, value.Length, flags, url));
        }

        public void Visit(Inline inline, SpanFlags flags, string? url)
        {
            switch (inline)
            {
                case TaskList:
                    break;

                case LiteralInline literal:
                    Append(literal.Content.ToString(), flags, url);
                    break;

                case EmphasisInline emphasis:
                {
                    var inner = flags;
                    if (emphasis.DelimiterChar is '*' or '_')
                        inner |= emphasis.DelimiterCount >= 2 ? SpanFlags.Bold : SpanFlags.Italic;
                    else if (emphasis.DelimiterChar == '~')
                        inner |= SpanFlags.Strike;

                    foreach (var child in emphasis)
                        Visit(child, inner, url);
                    break;
                }

                case CodeInline code:
                    Append(code.Content, flags | SpanFlags.Code, url);
                    break;

                case LinkInline link:
                {
                    if (link.IsImage)
                    {
                        var alt = link.FirstChild?.ToString() ?? link.Url ?? "";
                        Append($"[Image: {alt}]", flags | SpanFlags.Italic | SpanFlags.Muted, url);
                        break;
                    }

                    var target = link.Url ?? "";
                    var before = _text.Length;
                    foreach (var child in link)
                        Visit(child, flags | SpanFlags.Link, target);
                    if (_text.Length == before && target.Length > 0)
                        Append(target, flags | SpanFlags.Link, target);
                    break;
                }

                case AutolinkInline auto:
                    Append(auto.Url, flags | SpanFlags.Link, auto.Url);
                    break;

                case LineBreakInline:
                    Append("\n", flags, url);
                    break;

                case HtmlInline html:
                {
                    // Tags are dropped; any remaining text is kept.
                    var stripped = Regex.Replace(html.Tag, "<[^>]+>", "");
                    Append(stripped, flags, url);
                    break;
                }

                case HtmlEntityInline entity:
                    Append(entity.Transcoded.ToString(), flags, url);
                    break;

                case ContainerInline container:
                    foreach (var child in container)
                        Visit(child, flags, url);
                    break;

                default:
                    Append(inline.ToString() ?? "", flags, url);
                    break;
            }
        }
    }
}
