using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace GithubMarkdownViewer.Preview;

/// <summary>Creates <see cref="IPreviewText"/> values backed by Avalonia's <see cref="TextLayout"/>.</summary>
public sealed class AvaloniaTextProvider : ITextProvider
{
    private readonly PreviewStyle _style;
    private readonly PreviewPalette _palette;
    private readonly FontFamily _bodyFamily;
    private readonly FontFamily _monoFamily;

    public AvaloniaTextProvider(PreviewStyle style, PreviewPalette palette)
    {
        _style = style;
        _palette = palette;
        _bodyFamily = new FontFamily($"{style.FontFamilyName}, Inter, Segoe UI, Noto Sans, Helvetica, Arial, sans-serif");
        _monoFamily = new FontFamily($"{style.FontFamilyName}, Cascadia Code, Consolas, Menlo, Monaco, Courier New, monospace");
    }

    public IPreviewText Create(RichText text, TextRole role, double maxWidth, TextAlign align = TextAlign.Left)
    {
        var (size, weight, lineHeight, mono) = Describe(role);
        var family = mono ? _monoFamily : _bodyFamily;
        var typeface = new Typeface(family, FontStyle.Normal, weight);

        var foreground = role.Kind switch
        {
            TextKind.Code => _palette.CodeForeground,
            TextKind.Html => _palette.QuoteForeground,
            _ => role.Muted ? _palette.QuoteForeground : _palette.Foreground,
        };

        var overrides = BuildOverrides(text, typeface, size, foreground);
        var wrap = double.IsInfinity(maxWidth) ? TextWrapping.NoWrap : TextWrapping.Wrap;
        var textAlignment = align switch
        {
            TextAlign.Center => TextAlignment.Center,
            TextAlign.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };

        // letterSpacing must be 0: NaN makes every measured width NaN.
        var layout = new TextLayout(text.Text, typeface, size, foreground, textAlignment, wrap, TextTrimming.None,
            null, FlowDirection.LeftToRight, maxWidth, double.PositiveInfinity, lineHeight, 0, 0, overrides);

        return new AvaloniaPreviewText(layout, text.Text.Length);
    }

    private (double Size, FontWeight Weight, double LineHeight, bool Mono) Describe(TextRole role)
    {
        var baseSize = _style.BaseFontSize;
        var bodyWeight = role.Strong ? FontWeight.SemiBold : _style.BaseFontWeight;

        return role.Kind switch
        {
            TextKind.Heading1 => (baseSize * 2.1, FontWeight.Bold, double.NaN, false),
            TextKind.Heading2 => (baseSize * 1.65, FontWeight.SemiBold, double.NaN, false),
            TextKind.Heading3 => (baseSize * 1.35, FontWeight.SemiBold, double.NaN, false),
            TextKind.Heading4 => (baseSize * 1.2, FontWeight.SemiBold, double.NaN, false),
            TextKind.Heading5 => (baseSize * 1.05, FontWeight.SemiBold, double.NaN, false),
            TextKind.Heading6 => (baseSize, FontWeight.SemiBold, double.NaN, false),
            TextKind.Code => (baseSize * 0.85, FontWeight.Regular, baseSize * 1.45, true),
            TextKind.Html => (baseSize * 0.85, FontWeight.Regular, double.NaN, true),
            _ => (baseSize, bodyWeight, baseSize * 1.6, false),
        };
    }

    private IReadOnlyList<ValueSpan<TextRunProperties>>? BuildOverrides(RichText text, Typeface baseTypeface,
        double baseSize, IBrush baseForeground)
    {
        if (text.Spans.Count == 0) return null;

        var result = new List<ValueSpan<TextRunProperties>>(text.Spans.Count);
        foreach (var span in text.Spans)
        {
            var flags = span.Flags;
            var typeface = baseTypeface;
            var size = baseSize;
            IBrush foreground = baseForeground;
            IBrush? background = null;

            if (flags.HasFlag(SpanFlags.Code))
            {
                typeface = new Typeface(_monoFamily, FontStyle.Normal, FontWeight.Regular);
                size = _style.BaseFontSize * 0.85;
                foreground = _palette.CodeForeground;
                background = _palette.InlineCodeBackground;
            }

            if (flags.HasFlag(SpanFlags.Bold) || flags.HasFlag(SpanFlags.Italic))
            {
                typeface = new Typeface(typeface.FontFamily,
                    flags.HasFlag(SpanFlags.Italic) ? FontStyle.Italic : typeface.Style,
                    flags.HasFlag(SpanFlags.Bold) ? FontWeight.Bold : typeface.Weight);
            }

            if (flags.HasFlag(SpanFlags.Muted)) foreground = _palette.QuoteForeground;
            if (flags.HasFlag(SpanFlags.Link)) foreground = _palette.LinkForeground;

            result.Add(new ValueSpan<TextRunProperties>(span.Start, span.Length,
                new GenericTextRunProperties(typeface, size, BuildDecorations(flags), foreground, background)));
        }
        return result;
    }

    private static TextDecorationCollection? BuildDecorations(SpanFlags flags)
    {
        var underline = flags.HasFlag(SpanFlags.Link);
        var strike = flags.HasFlag(SpanFlags.Strike);
        if (!underline && !strike) return null;

        var decorations = new TextDecorationCollection();
        if (underline) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Underline });
        if (strike) decorations.Add(new TextDecoration { Location = TextDecorationLocation.Strikethrough });
        return decorations;
    }

    private sealed class AvaloniaPreviewText : IPreviewText
    {
        private readonly TextLayout _layout;

        public AvaloniaPreviewText(TextLayout layout, int length)
        {
            _layout = layout;
            Length = length;
        }

        public double Width => double.IsNaN(_layout.Width) ? 0 : _layout.Width;
        public double Height => double.IsNaN(_layout.Height) ? 0 : _layout.Height;
        public int Length { get; }

        public int HitTest(Point point)
        {
            var hit = _layout.HitTestPoint(point);
            return Math.Clamp(hit.TextPosition, 0, Length);
        }

        public IReadOnlyList<Rect> GetRangeRects(int start, int length)
        {
            if (length <= 0 || start >= Length) return Array.Empty<Rect>();
            return _layout.HitTestTextRange(start, Math.Min(length, Length - start)).ToList();
        }

        public void Draw(DrawingContext context, Point origin) => _layout.Draw(context, origin);

        public void Dispose() => _layout.Dispose();
    }
}
