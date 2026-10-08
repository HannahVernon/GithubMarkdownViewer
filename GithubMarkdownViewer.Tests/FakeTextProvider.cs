using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using GithubMarkdownViewer.Preview;

namespace GithubMarkdownViewer.Tests;

/// <summary>
/// Monospace text with fixed metrics (8 px per character, 16 px lines, 24 px heading lines)
/// and greedy word wrapping. It lets layout and selection logic run without Avalonia's platform.
/// </summary>
public sealed class FakeTextProvider : ITextProvider
{
    public const double CharWidth = 8;
    public const double LineHeight = 16;
    public const double HeadingLineHeight = 24;

    public IPreviewText Create(RichText text, TextRole role, double maxWidth, TextAlign align = TextAlign.Left)
    {
        var isHeading = role.Kind is >= TextKind.Heading1 and <= TextKind.Heading6;
        var wrap = !double.IsInfinity(maxWidth) && role.Kind != TextKind.Code;
        var maxChars = wrap ? Math.Max(1, (int)(maxWidth / CharWidth)) : int.MaxValue;
        return new FakeText(text.Text, maxChars, isHeading ? HeadingLineHeight : LineHeight);
    }

    private sealed class FakeText : IPreviewText
    {
        private readonly List<(int Start, int Length)> _lines = new();
        private readonly double _lineHeight;

        public FakeText(string text, int maxChars, double lineHeight)
        {
            _lineHeight = lineHeight;
            Length = text.Length;

            var lineStart = 0;
            foreach (var paragraph in text.Split('\n'))
            {
                var start = lineStart;
                var length = 0;
                foreach (var word in SplitKeepingSpaces(paragraph))
                {
                    if (length > 0 && length + word.Length > maxChars && word.Trim().Length > 0)
                    {
                        _lines.Add((start, length));
                        start += length;
                        length = 0;
                    }
                    length += word.Length;
                }
                _lines.Add((start, length));
                lineStart += paragraph.Length + 1;
            }
        }

        private static IEnumerable<string> SplitKeepingSpaces(string value)
        {
            var current = new System.Text.StringBuilder();
            foreach (var c in value)
            {
                current.Append(c);
                if (c == ' ')
                {
                    yield return current.ToString();
                    current.Clear();
                }
            }
            if (current.Length > 0) yield return current.ToString();
        }

        public int Length { get; }
        public double Width => _lines.Max(l => l.Length) * CharWidth;
        public double Height => _lines.Count * _lineHeight;

        public int HitTest(Point point)
        {
            var line = Math.Clamp((int)(point.Y / _lineHeight), 0, _lines.Count - 1);
            var column = Math.Clamp((int)Math.Round(point.X / CharWidth), 0, _lines[line].Length);
            return Math.Min(_lines[line].Start + column, Length);
        }

        public IReadOnlyList<Rect> GetRangeRects(int start, int length)
        {
            var rects = new List<Rect>();
            var end = start + length;
            for (var i = 0; i < _lines.Count; i++)
            {
                var from = Math.Max(start, _lines[i].Start);
                var to = Math.Min(end, _lines[i].Start + _lines[i].Length);
                if (to > from)
                    rects.Add(new Rect((from - _lines[i].Start) * CharWidth, i * _lineHeight, (to - from) * CharWidth, _lineHeight));
            }
            return rects;
        }

        public void Draw(DrawingContext context, Point origin)
        {
        }

        public void Dispose()
        {
        }
    }
}
