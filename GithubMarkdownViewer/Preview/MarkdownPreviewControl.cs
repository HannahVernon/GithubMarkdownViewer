using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Markdig;

namespace GithubMarkdownViewer.Preview;

/// <summary>
/// Draws a Markdown document on a single surface. Hosted inside a ScrollViewer, it draws
/// only the boxes that are in view.
/// </summary>
public sealed class MarkdownPreviewControl : Control
{
    /// <summary>Space between the control edge and the document content.</summary>
    public const double ContentPadding = 20;

    private IReadOnlyList<DocNode> _nodes = Array.Empty<DocNode>();
    private PreviewStyle _style = new("Inter", 13.33, FontWeight.Regular, false);
    private LayoutResult? _layout;
    private double _layoutWidth = -1;
    private bool _layoutDark;
    private ScrollViewer? _scrollViewer;
    private readonly Dictionary<int, double> _codeScroll = new();

    public LayoutResult? CurrentLayout => _layout;

    public void Configure(string fontFamilyName, double baseFontSizePx, FontWeight baseFontWeight)
    {
        var weight = baseFontWeight == default ? FontWeight.Regular : baseFontWeight;
        if (_style.FontFamilyName == fontFamilyName && _style.BaseFontSize == baseFontSizePx && _style.BaseFontWeight == weight)
            return;

        _style = _style with { FontFamilyName = fontFamilyName, BaseFontSize = baseFontSizePx, BaseFontWeight = weight };
        InvalidateLayout();
    }

    public void SetMarkdown(string markdown, MarkdownPipeline pipeline)
    {
        _nodes = DocumentBuilder.Build(markdown, pipeline);
        _codeScroll.Clear();
        InvalidateLayout();
    }

    private void InvalidateLayout()
    {
        _layout?.Dispose();
        _layout = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private bool IsDark => ActualThemeVariant == ThemeVariant.Dark;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        EnsureLayout(width);
        return new Size(width, _layout!.Height + 2 * ContentPadding);
    }

    private void EnsureLayout(double width)
    {
        var dark = IsDark;
        if (_layout != null && Math.Abs(_layoutWidth - width) < 0.5 && _layoutDark == dark) return;

        _layout?.Dispose();
        var provider = new AvaloniaTextProvider(_style with { Dark = dark }, PreviewPalette.For(dark));
        _layout = new LayoutEngine(provider).Layout(_nodes, width - 2 * ContentPadding);
        _layoutWidth = width;
        _layoutDark = dark;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property.Name == nameof(ActualThemeVariant))
            InvalidateLayout();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scrollViewer = this.FindAncestorOfType<ScrollViewer>();
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged += OnScrollChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer = null;
        _layout?.Dispose();
        _layout = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => InvalidateVisual();

    private Rect VisibleRect()
    {
        if (_scrollViewer == null) return new Rect(Bounds.Size);
        var offset = _scrollViewer.Offset;
        var viewport = _scrollViewer.Viewport;
        return new Rect(offset.X, offset.Y, viewport.Width, viewport.Height).Inflate(32);
    }

    private static Rect Shift(Rect rect) => rect.Translate(new Vector(ContentPadding, ContentPadding));

    public override void Render(DrawingContext context)
    {
        // Measure may not have run yet if the control has not been laid out.
        if (_layout == null || Bounds.Width <= 0) return;

        var palette = PreviewPalette.For(_layoutDark);
        var visible = VisibleRect();

        foreach (var box in _layout.Boxes)
        {
            var bounds = Shift(box.Bounds);
            if (!bounds.Intersects(visible)) continue;

            switch (box)
            {
                case FillBox fill:
                    DrawFill(context, palette, fill, bounds);
                    break;
                case TextLayoutBox text:
                    text.Text.Draw(context, bounds.TopLeft);
                    break;
                case CheckMarkBox check:
                    DrawCheckMark(context, palette, check, bounds);
                    break;
                case CodeBlockBox code:
                    DrawCodeBlock(context, palette, code, bounds);
                    break;
            }
        }
    }

    private static void DrawFill(DrawingContext context, PreviewPalette palette, FillBox fill, Rect bounds)
    {
        var brush = palette.Get(fill.Fill);
        var strokeBrush = palette.Get(fill.Stroke);
        var pen = strokeBrush == null ? null : new Pen(strokeBrush, 1);
        var rect = pen == null ? bounds : bounds.Deflate(0.5);
        context.DrawRectangle(brush, pen, rect, fill.CornerRadius, fill.CornerRadius);
    }

    private static void DrawCheckMark(DrawingContext context, PreviewPalette palette, CheckMarkBox check, Rect bounds)
    {
        var box = bounds.Deflate(1);
        var border = new Pen(palette.QuoteForeground, 1.5);
        context.DrawRectangle(check.Checked ? palette.LinkForeground : null, border, box, 3, 3);

        if (!check.Checked) return;
        var tick = new Pen(Brushes.White, 2, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        context.DrawLine(tick, new Point(box.X + box.Width * 0.25, box.Y + box.Height * 0.55),
            new Point(box.X + box.Width * 0.43, box.Y + box.Height * 0.72));
        context.DrawLine(tick, new Point(box.X + box.Width * 0.43, box.Y + box.Height * 0.72),
            new Point(box.X + box.Width * 0.76, box.Y + box.Height * 0.3));
    }

    private void DrawCodeBlock(DrawingContext context, PreviewPalette palette, CodeBlockBox code, Rect bounds)
    {
        context.DrawRectangle(palette.CodeBackground, new Pen(palette.CodeBorder, 1), bounds.Deflate(0.5), 6, 6);

        var scrollX = Math.Clamp(_codeScroll.GetValueOrDefault(code.Id), 0, code.MaxScrollX);
        var inner = new Rect(bounds.X + CodeBlockBox.Padding, bounds.Y, code.ViewportWidth, bounds.Height);

        using (context.PushClip(inner))
        {
            var contentOrigin = Shift(code.Content.Bounds).TopLeft - new Vector(scrollX, 0);
            code.Content.Text.Draw(context, contentOrigin);
        }

        if (code.MaxScrollX <= 0) return;

        // Thin scroll indicator along the bottom edge of the block.
        var trackWidth = code.ViewportWidth;
        var thumbWidth = Math.Max(24, trackWidth * trackWidth / (trackWidth + code.MaxScrollX));
        var thumbX = inner.X + (trackWidth - thumbWidth) * (scrollX / code.MaxScrollX);
        context.DrawRectangle(palette.ScrollThumb, null,
            new Rect(thumbX, bounds.Bottom - 7, thumbWidth, 4), 2, 2);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_layout == null) return;

        var sideways = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y);
        if (!sideways) return;

        var point = e.GetPosition(this) - new Point(ContentPadding, ContentPadding);
        foreach (var box in _layout.Boxes)
        {
            if (box is not CodeBlockBox code || !code.Bounds.Contains(point) || code.MaxScrollX <= 0) continue;

            var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
            var current = _codeScroll.GetValueOrDefault(code.Id);
            _codeScroll[code.Id] = Math.Clamp(current - delta * 48, 0, code.MaxScrollX);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
    }
}
