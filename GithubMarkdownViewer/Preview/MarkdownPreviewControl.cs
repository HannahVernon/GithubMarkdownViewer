using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Markdig;

namespace GithubMarkdownViewer.Preview;

/// <summary>
/// Draws a Markdown document on a single surface. Hosted inside a ScrollViewer, it draws
/// only the boxes that are in view. Selection works across blocks.
/// </summary>
public sealed class MarkdownPreviewControl : Control
{
    /// <summary>Space between the control edge and the document content.</summary>
    public const double ContentPadding = 20;

    private const double DragThreshold = 4;
    private const double AutoScrollMaxStep = 40;

    private IReadOnlyList<DocNode> _nodes = Array.Empty<DocNode>();
    private PreviewStyle _style = new("Inter", 13.33, FontWeight.Regular, false);
    private LayoutResult? _layout;
    private double _layoutWidth = -1;
    private bool _layoutDark;
    private ScrollViewer? _scrollViewer;
    private readonly Dictionary<int, double> _codeScroll = new();

    private DocPosition _anchor;
    private DocPosition _caret;
    private bool _hasSelection;
    private bool _dragging;
    private Point _pressPoint;
    private string? _pressedLink;
    private Point _lastPointerInViewport;
    private DispatcherTimer? _autoScrollTimer;
    private string? _hoverLink;

    public MarkdownPreviewControl()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Ibeam);
    }

    /// <summary>Raised when the user clicks a link. The argument is the link target as written in the document.</summary>
    public event Action<string>? LinkClicked;

    /// <summary>Raised when the layout is recomputed (new text, width, font, or theme). Positions in <see cref="CurrentLayout"/> have changed.</summary>
    public event Action? LayoutUpdated;

    public LayoutResult? CurrentLayout => _layout;

    /// <summary>
    /// Returns the current layout, computing it now if it is out of date. Returns null until the
    /// control has a width (it has not been laid out yet).
    /// </summary>
    public LayoutResult? GetLayout()
    {
        if (_layout == null && Bounds.Width > 0)
            EnsureLayout(Bounds.Width);
        return _layout;
    }

    public bool HasSelection => _hasSelection && _anchor != _caret;

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
        ClearSelection();
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

    // ── Layout ────────────────────────────────────────────────────

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = double.IsInfinity(availableSize.Width) ? 800 : availableSize.Width;
        EnsureLayout(width);

        // Fill the viewport so a click in the blank area below the text still reaches the control.
        var viewportHeight = _scrollViewer?.Viewport.Height ?? 0;
        return new Size(width, Math.Max(_layout!.Height + 2 * ContentPadding, viewportHeight));
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

        // The structure is the same after a resize or theme change, but drop the selection if it no longer fits.
        if (_hasSelection && (_anchor.Box >= _layout.TextBoxes.Count || _caret.Box >= _layout.TextBoxes.Count))
            ClearSelection();

        LayoutUpdated?.Invoke();
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
        StopAutoScroll();
        if (_scrollViewer != null)
            _scrollViewer.ScrollChanged -= OnScrollChanged;
        _scrollViewer = null;
        _layout?.Dispose();
        _layout = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // A new viewport height changes how tall the control should be.
        if (e.ViewportDelta.Y != 0) InvalidateMeasure();
        InvalidateVisual();
    }

    private Rect VisibleRect()
    {
        if (_scrollViewer == null) return new Rect(Bounds.Size);
        var offset = _scrollViewer.Offset;
        var viewport = _scrollViewer.Viewport;
        return new Rect(offset.X, offset.Y, viewport.Width, viewport.Height).Inflate(32);
    }

    private static Rect Shift(Rect rect) => rect.Translate(new Vector(ContentPadding, ContentPadding));

    private static Point ToContent(Point controlPoint) => controlPoint - new Point(ContentPadding, ContentPadding);

    private double CodeScrollX(CodeBlockBox block) =>
        Math.Clamp(_codeScroll.GetValueOrDefault(block.Id), 0, block.MaxScrollX);

    // ── Drawing ───────────────────────────────────────────────────

    public override void Render(DrawingContext context)
    {
        // Avalonia hit-tests custom controls against what they draw, so cover the whole control with
        // a transparent fill. Without it, clicks in blank areas would pass through to the ScrollViewer.
        context.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));

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
                    DrawSelection(context, palette, text, bounds.TopLeft);
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

    private void DrawSelection(DrawingContext context, PreviewPalette palette, TextLayoutBox box, Point origin)
    {
        if (!HasSelection || box.Index < 0) return;

        var (start, end) = Ordered();
        var range = DocumentSelection.RangeIn(box.Index, box.Text.Length, start, end);
        if (range == null) return;

        var (from, to) = range.Value;
        if (to > from)
        {
            foreach (var rect in box.Text.GetRangeRects(from, to - from))
                context.DrawRectangle(palette.SelectionBackground, null, rect.Translate(new Vector(origin.X, origin.Y)));
        }
        else if (box.Text.Length == 0 || (box.Index > start.Box && box.Index < end.Box))
        {
            // An empty cell or line inside the selection still shows a small marker.
            context.DrawRectangle(palette.SelectionBackground, null, new Rect(origin.X, origin.Y, 4, Math.Max(box.Text.Height, 12)));
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

        var scrollX = CodeScrollX(code);
        var inner = new Rect(bounds.X + CodeBlockBox.Padding, bounds.Y, code.ViewportWidth, bounds.Height);

        using (context.PushClip(inner))
        {
            var contentOrigin = Shift(code.Content.Bounds).TopLeft - new Vector(scrollX, 0);
            DrawSelection(context, palette, code.Content, contentOrigin);
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

    // ── Selection ─────────────────────────────────────────────────

    private (DocPosition Start, DocPosition End) Ordered() =>
        _anchor <= _caret ? (_anchor, _caret) : (_caret, _anchor);

    public void ClearSelection()
    {
        if (!_hasSelection) return;
        _hasSelection = false;
        _anchor = _caret = default;
        InvalidateVisual();
    }

    public void SelectAll()
    {
        if (_layout == null) return;
        if (DocumentSelection.SelectAll(_layout) is not { } all) return;

        _anchor = all.Start;
        _caret = all.End;
        _hasSelection = true;
        InvalidateVisual();
    }

    public string GetSelectedText()
    {
        if (_layout == null || !HasSelection) return "";
        var (start, end) = Ordered();
        return DocumentSelection.ExtractText(_layout, start, end);
    }

    public async Task CopySelectionAsync()
    {
        var text = GetSelectedText();
        if (text.Length == 0) return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard == null) return;
#pragma warning disable CS0618
        await clipboard.SetTextAsync(text);
#pragma warning restore CS0618
    }

    // ── Pointer input ─────────────────────────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_layout == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();
        var point = ToContent(e.GetPosition(this));
        if (DocumentSelection.PositionAt(_layout, point, CodeScrollX) is not { } position) return;

        _pressPoint = point;
        _pressedLink = DocumentSelection.LinkAt(_layout, point, CodeScrollX);

        if (e.ClickCount == 2)
        {
            var word = DocumentSelection.WordAt(_layout, position);
            (_anchor, _caret) = word;
        }
        else if (e.ClickCount >= 3)
        {
            var block = DocumentSelection.BlockAt(_layout, position);
            (_anchor, _caret) = block;
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && _hasSelection)
        {
            _caret = position;
        }
        else
        {
            _anchor = _caret = position;
        }

        _hasSelection = true;
        _dragging = true;
        _lastPointerInViewport = _scrollViewer != null ? e.GetPosition(_scrollViewer) : e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_layout == null) return;

        var point = ToContent(e.GetPosition(this));
        if (_dragging)
        {
            _lastPointerInViewport = _scrollViewer != null ? e.GetPosition(_scrollViewer) : e.GetPosition(this);
            ExtendSelectionTo(point);
            UpdateAutoScroll();
        }
        else
        {
            UpdateHover(point);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging) return;

        _dragging = false;
        StopAutoScroll();
        e.Pointer.Capture(null);

        var point = ToContent(e.GetPosition(this));
        var moved = Math.Sqrt(Math.Pow(point.X - _pressPoint.X, 2) + Math.Pow(point.Y - _pressPoint.Y, 2));
        if (moved < DragThreshold && _pressedLink != null && _layout != null
            && DocumentSelection.LinkAt(_layout, point, CodeScrollX) == _pressedLink)
        {
            var url = _pressedLink;
            _pressedLink = null;
            ClearSelection();
            LinkClicked?.Invoke(url);
        }
        _pressedLink = null;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (!_dragging) SetHover(null);
    }

    private void ExtendSelectionTo(Point contentPoint)
    {
        if (_layout == null) return;
        if (DocumentSelection.PositionAt(_layout, contentPoint, CodeScrollX) is not { } position) return;
        if (position == _caret) return;

        _caret = position;
        InvalidateVisual();
    }

    private void UpdateHover(Point point)
    {
        SetHover(_layout == null ? null : DocumentSelection.LinkAt(_layout, point, CodeScrollX));
    }

    private void SetHover(string? url)
    {
        if (url == _hoverLink) return;

        _hoverLink = url;
        Cursor = new Cursor(url != null ? StandardCursorType.Hand : StandardCursorType.Ibeam);
        ToolTip.SetTip(this, url);
        ToolTip.SetShowDelay(this, 400);
    }

    // ── Auto-scroll while dragging a selection past the edge ──────

    private double AutoScrollSpeed()
    {
        if (_scrollViewer == null) return 0;

        var y = _lastPointerInViewport.Y;
        var height = _scrollViewer.Viewport.Height;
        if (y < 0) return Math.Max(-AutoScrollMaxStep, y / 2);
        if (y > height) return Math.Min(AutoScrollMaxStep, (y - height) / 2);
        return 0;
    }

    private void UpdateAutoScroll()
    {
        if (AutoScrollSpeed() == 0)
        {
            StopAutoScroll();
            return;
        }

        if (_autoScrollTimer != null) return;
        _autoScrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
        _autoScrollTimer.Tick += OnAutoScrollTick;
        _autoScrollTimer.Start();
    }

    private void StopAutoScroll()
    {
        if (_autoScrollTimer == null) return;
        _autoScrollTimer.Stop();
        _autoScrollTimer.Tick -= OnAutoScrollTick;
        _autoScrollTimer = null;
    }

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        var speed = AutoScrollSpeed();
        if (_scrollViewer == null || !_dragging || speed == 0)
        {
            StopAutoScroll();
            return;
        }

        var offset = _scrollViewer.Offset;
        var maxY = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
        _scrollViewer.Offset = new Vector(offset.X, Math.Clamp(offset.Y + speed, 0, maxY));

        // The pointer has not moved but the content under it has.
        var pointInControl = _lastPointerInViewport + _scrollViewer.Offset;
        ExtendSelectionTo(ToContent(pointInControl));
    }

    // ── Wheel and keyboard ────────────────────────────────────────

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_layout == null) return;

        var sideways = e.KeyModifiers.HasFlag(KeyModifiers.Shift) || Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y);
        if (!sideways) return;

        var point = ToContent(e.GetPosition(this));
        foreach (var box in _layout.Boxes)
        {
            if (box is not CodeBlockBox code || !code.Bounds.Contains(point) || code.MaxScrollX <= 0) continue;

            var delta = Math.Abs(e.Delta.X) > Math.Abs(e.Delta.Y) ? e.Delta.X : e.Delta.Y;
            _codeScroll[code.Id] = Math.Clamp(CodeScrollX(code) - delta * 48, 0, code.MaxScrollX);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (ctrl && e.Key == Key.A)
        {
            SelectAll();
            e.Handled = true;
        }
        else if ((ctrl && e.Key == Key.C) || (ctrl && e.Key == Key.Insert))
        {
            _ = CopySelectionAsync();
            e.Handled = true;
        }
    }
}
