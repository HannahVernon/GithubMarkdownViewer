using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace GithubMarkdownViewer.Preview;

/// <summary>
/// Positions the document model into boxes in content coordinates. It has no Avalonia
/// platform dependency: text measurement comes from an <see cref="ITextProvider"/>.
/// </summary>
public sealed class LayoutEngine
{
    private const double ListIndent = 20;
    private const double MarkerGap = 6;
    private const double CheckMarkSize = 16;
    private const double QuoteBarWidth = 4;
    private const double QuotePadding = 16;
    private const double TableCellPadX = 10;
    private const double TableCellPadY = 6;
    private const double BlockGap = 12;

    private readonly ITextProvider _text;

    public LayoutEngine(ITextProvider text)
    {
        _text = text;
    }

    /// <param name="width">Content width, not including the control's padding.</param>
    public LayoutResult Layout(IReadOnlyList<DocNode> nodes, double width)
    {
        var pass = new Pass();
        var anchors = new List<BlockAnchor>();
        var end = LayoutNodes(pass, nodes, 0, 0, Math.Max(1, width), 0, default, anchors);

        return new LayoutResult
        {
            Height = end,
            Boxes = pass.Boxes,
            TextBoxes = pass.TextBoxes,
            Anchors = anchors,
            HeadingPositions = pass.Headings,
        };
    }

    private sealed class Pass
    {
        public readonly List<LayoutBox> Boxes = new();
        public readonly List<TextLayoutBox> TextBoxes = new();
        public readonly Dictionary<string, double> Headings = new(StringComparer.OrdinalIgnoreCase);
        public int CodeBlocks;

        /// <summary>Separator for the next selectable box, set by a list so its first item follows the block before it.</summary>
        public string? PendingSeparator;
    }

    private static string TakeSeparator(Pass pass, string fallback)
    {
        var separator = pass.PendingSeparator ?? fallback;
        pass.PendingSeparator = null;
        return separator;
    }

    /// <param name="Muted">Paragraphs and headings use the quote colour.</param>
    /// <param name="Tight">Directly inside a tight list item: no gap below paragraphs and nested lists.</param>
    /// <param name="Compact">Inside a list item: copied text separates blocks with one newline instead of a blank line.</param>
    private readonly record struct Context(bool Muted = false, bool Tight = false, bool Compact = false);

    private static string Separator(Context context) => context.Compact ? "\n" : "\n\n";

    /// <summary>The vertical extent of a placed node. <c>Next</c> includes the node's bottom margin.</summary>
    private readonly record struct Placed(double Top, double Bottom, double Next);

    private double LayoutNodes(Pass pass, IReadOnlyList<DocNode> nodes, double x, double y, double width,
        double spacing, Context context, List<BlockAnchor>? anchors)
    {
        var cursor = y;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (i > 0) cursor += spacing;
            var placed = LayoutNode(pass, nodes[i], x, cursor, width, context);
            anchors?.Add(new BlockAnchor(nodes[i].StartLine, nodes[i].EndLine, placed.Top, placed.Bottom - placed.Top));
            cursor = placed.Next;
        }
        return cursor;
    }

    private Placed LayoutNode(Pass pass, DocNode node, double x, double y, double width, Context context) => node switch
    {
        HeadingNode heading => LayoutHeading(pass, heading, x, y, width, context),
        ParagraphNode paragraph => LayoutParagraph(pass, paragraph, x, y, width, context),
        CodeNode code => LayoutCode(pass, code, x, y, width, context),
        QuoteNode quote => LayoutQuote(pass, quote, x, y, width),
        ListNode list => LayoutList(pass, list, x, y, width, context),
        RuleNode => LayoutRule(pass, x, y, width),
        TableNode table => LayoutTable(pass, table, x, y, width),
        HtmlNode html => LayoutHtml(pass, html, x, y, width, context),
        _ => new Placed(y, y, y),
    };

    private TextLayoutBox AddText(Pass pass, RichText source, TextRole role, double x, double y, double width,
        double maxWidth, TextAlign align = TextAlign.Left, bool selectable = true, string separator = "\n\n")
    {
        var text = _text.Create(source, role, maxWidth, align);
        var box = new TextLayoutBox
        {
            Text = text,
            Source = source,
            Selectable = selectable,
            SeparatorBefore = selectable ? TakeSeparator(pass, separator) : separator,
            Bounds = new Rect(x, y, width, text.Height),
        };
        pass.Boxes.Add(box);
        if (selectable)
        {
            box.Index = pass.TextBoxes.Count;
            pass.TextBoxes.Add(box);
        }
        return box;
    }

    private Placed LayoutHeading(Pass pass, HeadingNode heading, double x, double y, double width, Context context)
    {
        var level = Math.Clamp(heading.Level, 1, 6);
        var role = new TextRole(TextKind.Heading1 + (level - 1), context.Muted);

        var top = y + 16;
        if (!string.IsNullOrEmpty(heading.Id))
            pass.Headings.TryAdd(heading.Id, top);

        if (level <= 2)
        {
            var box = AddText(pass, heading.Text, role, x, top + 16, width, width, separator: Separator(context));
            var lineY = box.Bounds.Bottom + 8 + 6;
            pass.Boxes.Add(new FillBox
            {
                Bounds = new Rect(x, lineY, width, 1),
                Fill = PaletteColor.CodeBorder,
            });
            return new Placed(top, lineY + 1, lineY + 1 + 8);
        }

        var text = AddText(pass, heading.Text, role, x, top, width, width, separator: Separator(context));
        return new Placed(top, text.Bounds.Bottom, text.Bounds.Bottom + 8);
    }

    private Placed LayoutParagraph(Pass pass, ParagraphNode paragraph, double x, double y, double width, Context context)
    {
        var box = AddText(pass, paragraph.Text, new TextRole(TextKind.Body, context.Muted), x, y, width, width,
            separator: Separator(context));
        return new Placed(y, box.Bounds.Bottom, box.Bounds.Bottom + (context.Tight ? 0 : BlockGap));
    }

    private Placed LayoutHtml(Pass pass, HtmlNode html, double x, double y, double width, Context context)
    {
        var box = AddText(pass, RichText.Plain(html.Text), new TextRole(TextKind.Html), x, y, width, width,
            separator: Separator(context));
        return new Placed(y, box.Bounds.Bottom, box.Bounds.Bottom + BlockGap);
    }

    private Placed LayoutCode(Pass pass, CodeNode code, double x, double y, double width, Context context)
    {
        var source = RichText.Plain(code.Text);
        var text = _text.Create(source, new TextRole(TextKind.Code), double.PositiveInfinity);
        var height = text.Height + 2 * CodeBlockBox.Padding;

        var content = new TextLayoutBox
        {
            Text = text,
            Source = source,
            SeparatorBefore = TakeSeparator(pass, Separator(context)),
            Index = pass.TextBoxes.Count,
            Bounds = new Rect(x + CodeBlockBox.Padding, y + CodeBlockBox.Padding, text.Width, text.Height),
        };
        var block = new CodeBlockBox
        {
            Id = pass.CodeBlocks++,
            Bounds = new Rect(x, y, width, height),
            Content = content,
        };
        content.ScrollOwner = block;
        pass.Boxes.Add(block);
        pass.TextBoxes.Add(content);

        return new Placed(y, y + height, y + height + BlockGap);
    }

    private Placed LayoutQuote(Pass pass, QuoteNode quote, double x, double y, double width)
    {
        var contentX = x + QuoteBarWidth + QuotePadding;
        var contentWidth = Math.Max(1, width - QuoteBarWidth - QuotePadding);

        var end = LayoutNodes(pass, quote.Children, contentX, y + 4, contentWidth, 4, new Context(Muted: true), null);
        var bottom = end + 4;

        pass.Boxes.Add(new FillBox
        {
            Bounds = new Rect(x, y, QuoteBarWidth, bottom - y),
            Fill = PaletteColor.QuoteBorder,
        });
        return new Placed(y, bottom, bottom + BlockGap);
    }

    private Placed LayoutRule(Pass pass, double x, double y, double width)
    {
        var top = y + 16;
        pass.Boxes.Add(new FillBox
        {
            Bounds = new Rect(x, top, width, 3),
            Fill = PaletteColor.HrBackground,
            CornerRadius = 1.5,
        });
        return new Placed(top, top + 3, top + 3 + 16);
    }

    private Placed LayoutList(Pass pass, ListNode list, double x, double y, double width, Context context)
    {
        var body = new TextRole(TextKind.Body);

        // Marker column width is shared by all items so the text lines up.
        var markers = new string?[list.Items.Count];
        var markerWidth = list.Ordered ? 24.0 : 16.0;
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (list.Items[i].TaskChecked != null) continue;
            markers[i] = list.Ordered ? $"{list.Start + i}." : "\u2022";
            using var measure = _text.Create(RichText.Plain(markers[i]!), body, double.PositiveInfinity);
            markerWidth = Math.Max(markerWidth, measure.Width);
        }

        var contentX = x + ListIndent + markerWidth + MarkerGap;
        var contentWidth = Math.Max(40, width - ListIndent - markerWidth - MarkerGap);
        var itemContext = new Context(Tight: list.Tight, Compact: true);

        var cursor = y;
        for (var i = 0; i < list.Items.Count; i++)
        {
            if (i > 0) cursor += 4;
            var item = list.Items[i];

            double markerHeight;
            if (markers[i] is { } marker)
            {
                var box = AddText(pass, RichText.Plain(marker), body, x + ListIndent, cursor, markerWidth, double.PositiveInfinity,
                    selectable: false);
                box.Bounds = new Rect(x + ListIndent + markerWidth - box.Text.Width, cursor, box.Text.Width, box.Text.Height);
                markerHeight = box.Text.Height;
            }
            else
            {
                using var measure = _text.Create(RichText.Plain("X"), body, double.PositiveInfinity);
                markerHeight = measure.Height;
                pass.Boxes.Add(new CheckMarkBox
                {
                    Checked = item.TaskChecked == true,
                    Bounds = new Rect(x + ListIndent + markerWidth - CheckMarkSize, cursor + Math.Max(0, (markerHeight - CheckMarkSize) / 2),
                        CheckMarkSize, CheckMarkSize),
                });
            }

            // The first item follows whatever block precedes the list; later items start on the next line.
            if (i == 0) pass.PendingSeparator = Separator(context);
            var end = LayoutNodes(pass, item.Children, contentX, cursor, contentWidth, 2, itemContext, null);
            cursor = Math.Max(end, cursor + markerHeight);
        }

        return new Placed(y, cursor, cursor + (context.Tight ? 0 : BlockGap));
    }

    private Placed LayoutTable(Pass pass, TableNode table, double x, double y, double width)
    {
        var columns = table.Columns;
        if (table.Rows.Count == 0) return new Placed(y, y, y + BlockGap);

        var minWidths = new double[columns];
        var maxWidths = new double[columns];
        foreach (var row in table.Rows)
        {
            var role = new TextRole(TextKind.Body, Strong: row.IsHeader);
            for (var c = 0; c < Math.Min(columns, row.Cells.Count); c++)
            {
                var cell = row.Cells[c].Text;
                if (cell.Text.Length == 0) continue;

                using (var full = _text.Create(cell, role, double.PositiveInfinity))
                    maxWidths[c] = Math.Max(maxWidths[c], full.Width);

                var longest = cell.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                    .OrderByDescending(w => w.Length).FirstOrDefault();
                if (longest != null)
                {
                    using var word = _text.Create(RichText.Plain(longest), role, double.PositiveInfinity);
                    minWidths[c] = Math.Max(minWidths[c], word.Width);
                }
            }
        }

        for (var c = 0; c < columns; c++)
            maxWidths[c] = Math.Max(maxWidths[c], minWidths[c]);

        var available = Math.Max(columns * 8.0, width - columns * 2 * TableCellPadX - 1);
        var contentWidths = ColumnWidths(minWidths, maxWidths, available);

        var cursor = y;
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var row = table.Rows[r];
            var role = new TextRole(TextKind.Body, Strong: row.IsHeader);
            var fill = row.IsHeader ? PaletteColor.TableHeaderBackground
                : r % 2 == 0 ? PaletteColor.None : PaletteColor.TableAltRowBackground;

            var texts = new IPreviewText[columns];
            var rowHeight = 0.0;
            for (var c = 0; c < columns; c++)
            {
                var cell = c < row.Cells.Count ? row.Cells[c] : null;
                texts[c] = _text.Create(cell?.Text ?? RichText.Empty, role, contentWidths[c], ToTextAlign(cell?.Align ?? CellAlign.Left));
                rowHeight = Math.Max(rowHeight, texts[c].Height);
            }
            rowHeight += 2 * TableCellPadY;

            var cellX = x;
            for (var c = 0; c < columns; c++)
            {
                var cellWidth = contentWidths[c] + 2 * TableCellPadX;
                pass.Boxes.Add(new FillBox
                {
                    Bounds = new Rect(cellX, cursor, cellWidth, rowHeight),
                    Fill = fill,
                    Stroke = PaletteColor.TableBorder,
                });

                // First cell of the first row follows a block; other first cells start a new line; the rest are tab-separated.
                var separator = c > 0 ? "\t" : r == 0 ? TakeSeparator(pass, "\n\n") : "\n";
                var box = new TextLayoutBox
                {
                    Text = texts[c],
                    Source = c < row.Cells.Count ? row.Cells[c].Text : RichText.Empty,
                    SeparatorBefore = separator,
                    Index = pass.TextBoxes.Count,
                    Bounds = new Rect(cellX + TableCellPadX, cursor + TableCellPadY, contentWidths[c], texts[c].Height),
                };
                pass.Boxes.Add(box);
                pass.TextBoxes.Add(box);
                cellX += cellWidth;
            }
            cursor += rowHeight;
        }

        return new Placed(y, cursor, cursor + BlockGap);
    }

    /// <summary>
    /// Uses each column's natural width when everything fits. Otherwise starts from the longest word
    /// in each column and shares the remaining width in proportion to how much each column can grow.
    /// </summary>
    public static double[] ColumnWidths(double[] minWidths, double[] maxWidths, double available)
    {
        var count = minWidths.Length;
        var result = new double[count];
        var sumMax = maxWidths.Sum();
        var sumMin = minWidths.Sum();

        if (sumMax <= available)
        {
            Array.Copy(maxWidths, result, count);
        }
        else if (sumMin <= available)
        {
            var extra = available - sumMin;
            var growth = sumMax - sumMin;
            for (var c = 0; c < count; c++)
                result[c] = minWidths[c] + (growth > 0 ? extra * (maxWidths[c] - minWidths[c]) / growth : 0);
        }
        else
        {
            var scale = available / sumMin;
            for (var c = 0; c < count; c++)
                result[c] = Math.Max(8, minWidths[c] * scale);
        }

        return result;
    }

    private static TextAlign ToTextAlign(CellAlign align) => align switch
    {
        CellAlign.Center => TextAlign.Center,
        CellAlign.Right => TextAlign.Right,
        _ => TextAlign.Left,
    };
}
