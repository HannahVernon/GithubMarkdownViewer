using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;

namespace GithubMarkdownViewer.Preview;

/// <summary>A caret position in the document: which text box, and the character offset inside it.</summary>
public readonly record struct DocPosition(int Box, int Offset) : IComparable<DocPosition>
{
    public int CompareTo(DocPosition other) => Box != other.Box ? Box.CompareTo(other.Box) : Offset.CompareTo(other.Offset);

    public static bool operator <(DocPosition left, DocPosition right) => left.CompareTo(right) < 0;
    public static bool operator >(DocPosition left, DocPosition right) => left.CompareTo(right) > 0;
    public static bool operator <=(DocPosition left, DocPosition right) => left.CompareTo(right) <= 0;
    public static bool operator >=(DocPosition left, DocPosition right) => left.CompareTo(right) >= 0;
}

/// <summary>Hit testing, ranges and text extraction over a <see cref="LayoutResult"/>. No UI dependencies.</summary>
public static class DocumentSelection
{
    /// <summary>
    /// Maps a point in content coordinates to the nearest caret position. Points in the gaps between
    /// blocks, or outside the document, snap to the closest text box.
    /// </summary>
    public static DocPosition? PositionAt(LayoutResult layout, Point point, Func<CodeBlockBox, double>? codeScroll = null)
    {
        var boxes = layout.TextBoxes;
        if (boxes.Count == 0) return null;

        var best = 0;
        var bestDy = double.PositiveInfinity;
        var bestDx = double.PositiveInfinity;
        for (var i = 0; i < boxes.Count; i++)
        {
            var area = HitArea(boxes[i]);
            var dy = Gap(point.Y, area.Top, area.Bottom);
            var dx = Gap(point.X, area.Left, area.Right);
            if (dy < bestDy || (dy == bestDy && dx < bestDx))
            {
                best = i;
                bestDy = dy;
                bestDx = dx;
            }
        }

        var box = boxes[best];
        var local = ToLocal(box, point, codeScroll);
        return new DocPosition(best, Math.Clamp(box.Text.HitTest(local), 0, box.Text.Length));
    }

    /// <summary>The character under a point, or null when the point is not over a character.</summary>
    public static int? CharIndexAt(TextLayoutBox box, Point local)
    {
        var caret = box.Text.HitTest(local);
        foreach (var index in new[] { caret, caret - 1 })
        {
            if (index < 0 || index >= box.Text.Length) continue;
            foreach (var rect in box.Text.GetRangeRects(index, 1))
            {
                if (rect.Contains(local)) return index;
            }
        }
        return null;
    }

    /// <summary>The link URL under a point, or null.</summary>
    public static string? LinkAt(LayoutResult layout, Point point, Func<CodeBlockBox, double>? codeScroll = null)
    {
        foreach (var box in layout.TextBoxes)
        {
            if (box.ScrollOwner != null || !box.Bounds.Contains(point)) continue;

            var index = CharIndexAt(box, ToLocal(box, point, codeScroll));
            return index == null ? null : box.Source.UrlAt(index.Value);
        }
        return null;
    }

    /// <summary>Selected character range inside one box, or null when the box is outside the selection.</summary>
    public static (int Start, int End)? RangeIn(int boxIndex, int length, DocPosition start, DocPosition end)
    {
        if (start >= end || boxIndex < start.Box || boxIndex > end.Box) return null;

        var from = boxIndex == start.Box ? start.Offset : 0;
        var to = boxIndex == end.Box ? end.Offset : length;
        return (Math.Clamp(from, 0, length), Math.Clamp(to, 0, length));
    }

    public static (DocPosition Start, DocPosition End)? SelectAll(LayoutResult layout)
    {
        if (layout.TextBoxes.Count == 0) return null;
        var last = layout.TextBoxes.Count - 1;
        return (new DocPosition(0, 0), new DocPosition(last, layout.TextBoxes[last].Text.Length));
    }

    public static (DocPosition Start, DocPosition End) WordAt(LayoutResult layout, DocPosition position)
    {
        var box = layout.TextBoxes[position.Box];
        var (start, end) = WordAt(box.Source.Text, position.Offset);
        return (new DocPosition(position.Box, start), new DocPosition(position.Box, end));
    }

    public static (DocPosition Start, DocPosition End) BlockAt(LayoutResult layout, DocPosition position) =>
        (new DocPosition(position.Box, 0), new DocPosition(position.Box, layout.TextBoxes[position.Box].Text.Length));

    /// <summary>Finds the run of word characters, spaces, or the single symbol around an index.</summary>
    public static (int Start, int End) WordAt(string text, int index)
    {
        if (text.Length == 0) return (0, 0);

        var i = Math.Clamp(index, 0, text.Length - 1);
        var kind = Classify(text[i]);
        if (kind == CharKind.Symbol) return (i, i + 1);

        var start = i;
        while (start > 0 && Classify(text[start - 1]) == kind) start--;
        var end = i + 1;
        while (end < text.Length && Classify(text[end]) == kind) end++;
        return (start, end);
    }

    /// <summary>The selected text, with each box's separator between boxes.</summary>
    public static string ExtractText(LayoutResult layout, DocPosition start, DocPosition end)
    {
        if (start >= end) return "";

        var result = new StringBuilder();
        var first = true;
        for (var i = start.Box; i <= end.Box && i < layout.TextBoxes.Count; i++)
        {
            var box = layout.TextBoxes[i];
            var length = box.Text.Length;
            var range = RangeIn(i, length, start, end);
            if (range == null) continue;
            var (from, to) = range.Value;

            // A selection that only touches the edge of a box does not include that box.
            if (i != end.Box && from >= length && i == start.Box) continue;
            if (i != start.Box && to == 0 && i == end.Box) continue;

            if (!first) result.Append(box.SeparatorBefore);
            result.Append(box.Source.Text, from, to - from);
            first = false;
        }
        return result.ToString();
    }

    private static Rect HitArea(TextLayoutBox box) => box.ScrollOwner?.Bounds ?? box.Bounds;

    private static double Gap(double value, double low, double high) =>
        value < low ? low - value : value > high ? value - high : 0;

    private static Point ToLocal(TextLayoutBox box, Point point, Func<CodeBlockBox, double>? codeScroll)
    {
        var scrollX = box.ScrollOwner != null && codeScroll != null ? codeScroll(box.ScrollOwner) : 0;
        return new Point(point.X - box.Bounds.X + scrollX, point.Y - box.Bounds.Y);
    }

    private enum CharKind
    {
        Word,
        Space,
        Symbol,
    }

    private static CharKind Classify(char c) =>
        char.IsLetterOrDigit(c) || c == '_' ? CharKind.Word
        : char.IsWhiteSpace(c) ? CharKind.Space
        : CharKind.Symbol;
}
