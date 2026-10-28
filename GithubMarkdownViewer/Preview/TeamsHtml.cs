using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace GithubMarkdownViewer.Preview;

/// <summary>
/// Writes the document, or the selected part of it, as HTML that pastes well into a Microsoft Teams message.
/// The rules come from paste tests in Teams:
/// <list type="bullet">
/// <item>Teams removes paragraph margins, so an empty paragraph separates top-level blocks.  There is no
/// gap after a heading (except the title) or before or after a list, which already has its own spacing.</item>
/// <item>Teams shows a level 1 heading in a plain weight, so it is sent as level 2, which Teams shows bold.
/// Levels 4 to 6 become bold paragraphs, because Teams offers only three heading levels.</item>
/// <item>No inline styles: Teams styles tables, code blocks and inline code itself.</item>
/// </list>
/// The writer walks the nodes in the same order that <see cref="LayoutEngine"/> creates selectable text
/// boxes, so a <see cref="DocPosition"/> range maps onto the nodes without a lookup table.
/// </summary>
public static class TeamsHtml
{
    private const string Gap = "<p>&nbsp;</p>";

    /// <summary>Writes the whole document, or only the range from <paramref name="start"/> to <paramref name="end"/>.</summary>
    public static string Write(IReadOnlyList<DocNode> nodes, DocPosition? start = null, DocPosition? end = null)
    {
        var writer = new Writer(start, end);
        var blocks = writer.Blocks(nodes);

        var html = new StringBuilder();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (i > 0 && NeedsGap(blocks[i - 1].Node, blocks[i].Node)) html.Append(Gap).Append('\n');
            html.Append(blocks[i].Html).Append('\n');
        }
        return html.ToString();
    }

    private static bool NeedsGap(DocNode previous, DocNode next) =>
        previous is not ListNode && next is not ListNode
        && !(previous is HeadingNode heading && heading.Level > 1);

    private readonly record struct Block(DocNode Node, string Html);

    private sealed class Writer
    {
        private readonly DocPosition _start;
        private readonly DocPosition _end;
        private readonly bool _all;
        private int _box;
        private bool _emitted;

        public Writer(DocPosition? start, DocPosition? end)
        {
            _all = start == null || end == null;
            _start = start ?? default;
            _end = end ?? default;
        }

        public List<Block> Blocks(IReadOnlyList<DocNode> nodes)
        {
            var blocks = new List<Block>();
            foreach (var node in nodes)
            {
                var html = Node(node, tight: false);
                if (html.Length > 0) blocks.Add(new Block(node, html));
            }
            return blocks;
        }

        private string Node(DocNode node, bool tight) => node switch
        {
            HeadingNode heading => Heading(heading),
            ParagraphNode paragraph => Paragraph(paragraph.Text, tight),
            CodeNode code => Code(code.Text),
            HtmlNode html => Code(html.Text),
            QuoteNode quote => Quote(quote),
            ListNode list => List(list),
            TableNode table => Table(table),
            RuleNode => Rule(),
            _ => "",
        };

        /// <summary>
        /// The selected part of the next text box, or null when it is outside the selection. Consumes one
        /// box, so every node that has a box in the layout must call this exactly once per box.
        /// </summary>
        private (int From, int To)? Take(int length)
        {
            var index = _box++;
            if (_all) return Emit((0, length));

            var range = DocumentSelection.RangeIn(index, length, _start, _end);
            if (range is not { } r) return null;

            // Same rule as DocumentSelection.ExtractText: touching only the edge of a box does not include it.
            if (index == _start.Box && index != _end.Box && r.Start >= length) return null;
            if (index == _end.Box && index != _start.Box && r.End == 0) return null;
            return Emit(r);
        }

        private (int From, int To) Emit((int From, int To) range)
        {
            _emitted = true;
            return range;
        }

        private string Heading(HeadingNode heading)
        {
            if (Take(heading.Text.Text.Length) is not { } range) return "";
            var inline = Inline(heading.Text, range);
            if (inline.Length == 0) return "";

            return heading.Level switch
            {
                <= 2 => $"<h2>{inline}</h2>",
                3 => $"<h3>{inline}</h3>",
                _ => $"<p><strong>{inline}</strong></p>",
            };
        }

        private string Paragraph(RichText text, bool tight)
        {
            if (Take(text.Text.Length) is not { } range) return "";
            var inline = Inline(text, range);
            if (inline.Length == 0) return "";
            return tight ? inline : $"<p>{inline}</p>";
        }

        private string Code(string text)
        {
            if (Take(text.Length) is not { } range) return "";
            var code = text[range.From..range.To];
            if (code.Length == 0) return "";
            return $"<pre><code>{Escape(code)}</code></pre>";
        }

        private string Quote(QuoteNode quote)
        {
            var inner = string.Concat(quote.Children.Select(child => Node(child, tight: false)));
            return inner.Length == 0 ? "" : $"<blockquote>{inner}</blockquote>";
        }

        private string Rule()
        {
            // A rule has no text box.  Include it when it sits between selected text.
            if (_all) return "<hr>";
            var next = _box;
            var nextSelected = next < _end.Box || (next == _end.Box && _end.Offset > 0);
            return _emitted && nextSelected ? "<hr>" : "";
        }

        private string List(ListNode list)
        {
            var items = new StringBuilder();
            int? firstNumber = null;
            for (var i = 0; i < list.Items.Count; i++)
            {
                var item = list.Items[i];
                var content = string.Concat(item.Children.Select(child => Node(child, list.Tight)));
                if (content.Length == 0) continue;

                firstNumber ??= list.Start + i;
                var check = item.TaskChecked switch
                {
                    true => "\u2611 ",
                    false => "\u2610 ",
                    null => "",
                };
                items.Append("<li>").Append(check).Append(content).Append("</li>");
            }
            if (items.Length == 0) return "";

            if (!list.Ordered) return $"<ul>{items}</ul>";
            return firstNumber is { } number && number != 1 ? $"<ol start=\"{number}\">{items}</ol>" : $"<ol>{items}</ol>";
        }

        private string Table(TableNode table)
        {
            var rows = new List<(bool Header, string Html)>();
            var cellCount = 0;
            var lastCell = "";
            foreach (var row in table.Rows)
            {
                var cells = new StringBuilder();
                var any = false;
                for (var c = 0; c < table.Columns; c++)
                {
                    var cell = c < row.Cells.Count ? row.Cells[c] : null;
                    var text = cell?.Text ?? RichText.Empty;
                    var inline = "";
                    if (Take(text.Text.Length) is { } range)
                    {
                        any = true;
                        cellCount++;
                        inline = Inline(text, range);
                        lastCell = inline;
                    }

                    var tag = row.IsHeader ? "th" : "td";
                    var align = cell?.Align switch
                    {
                        CellAlign.Center => " style=\"text-align:center\"",
                        CellAlign.Right => " style=\"text-align:right\"",
                        _ => "",
                    };
                    cells.Append('<').Append(tag).Append(align).Append('>').Append(inline).Append("</").Append(tag).Append('>');
                }
                if (any) rows.Add((row.IsHeader, $"<tr>{cells}</tr>"));
            }

            if (rows.Count == 0) return "";
            // Part of a single cell reads better as text than as a one-cell table.
            if (cellCount == 1) return lastCell.Length == 0 ? "" : $"<p>{lastCell}</p>";

            var html = new StringBuilder("<table>");
            var head = rows.Where(r => r.Header).ToList();
            var body = rows.Where(r => !r.Header).ToList();
            if (head.Count > 0) html.Append("<thead>").AppendJoin("", head.Select(r => r.Html)).Append("</thead>");
            if (body.Count > 0) html.Append("<tbody>").AppendJoin("", body.Select(r => r.Html)).Append("</tbody>");
            return html.Append("</table>").ToString();
        }
    }

    /// <summary>The characters from <c>From</c> to <c>To</c> of a rich text, with their bold, italic, strike, code and link styles.</summary>
    internal static string Inline(RichText text, (int From, int To) range)
    {
        var (from, to) = range;
        if (from >= to) return "";

        var html = new StringBuilder();
        var position = from;
        foreach (var span in text.Spans.OrderBy(s => s.Start))
        {
            var spanFrom = Math.Max(span.Start, from);
            var spanTo = Math.Min(span.End, to);
            if (spanFrom >= spanTo) continue;

            if (spanFrom > position) html.Append(Text(text.Text[position..spanFrom]));
            html.Append(Styled(text.Text[spanFrom..spanTo], span));
            position = spanTo;
        }
        if (position < to) html.Append(Text(text.Text[position..to]));
        return html.ToString();
    }

    private static string Styled(string value, TextSpan span)
    {
        var html = Text(value);
        if (span.Flags.HasFlag(SpanFlags.Code)) html = $"<code>{html}</code>";
        if (span.Flags.HasFlag(SpanFlags.Strike)) html = $"<s>{html}</s>";
        if (span.Flags.HasFlag(SpanFlags.Italic)) html = $"<em>{html}</em>";
        if (span.Flags.HasFlag(SpanFlags.Bold)) html = $"<strong>{html}</strong>";
        if (span.Flags.HasFlag(SpanFlags.Link) && IsShareableUrl(span.Url))
            html = $"<a href=\"{Escape(span.Url!)}\">{html}</a>";
        return html;
    }

    /// <summary>
    /// Web and mail links work for the person who receives the message.  Relative and in-page links point
    /// at files on this computer, so they become plain text.
    /// </summary>
    public static bool IsShareableUrl(string? url) =>
        ContextMenuPlan.IsWebUrl(url)
        || (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeMailto);

    private static string Text(string value) => Escape(value).Replace("\n", "<br>");

    private static string Escape(string value)
    {
        var html = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            html.Append(c switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                _ => c.ToString(),
            });
        }
        return html.ToString();
    }
}
