using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Renders yellow background rectangles behind find-match ranges in the AvaloniaEdit TextEditor.
/// </summary>
public class FindHighlightRenderer : IBackgroundRenderer
{
    private readonly List<TextSegment> _segments = new();

    public IBrush MatchBrush { get; set; } = new SolidColorBrush(Color.Parse("#FBC02D"));
    public IBrush CurrentMatchBrush { get; set; } = new SolidColorBrush(Color.Parse("#FF6F00"));
    public int CurrentMatchIndex { get; set; } = -1;

    public KnownLayer Layer => KnownLayer.Selection;

    public void SetMatches(IEnumerable<TextSegment> segments)
    {
        _segments.Clear();
        _segments.AddRange(segments);
    }

    public void ClearMatches()
    {
        _segments.Clear();
        CurrentMatchIndex = -1;
    }

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_segments.Count == 0) return;

        var builder = new BackgroundGeometryBuilder
        {
            CornerRadius = 2
        };

        for (int i = 0; i < _segments.Count; i++)
        {
            var segment = _segments[i];
            bool isCurrent = i == CurrentMatchIndex;

            var geoBuilder = new BackgroundGeometryBuilder { CornerRadius = 2 };
            geoBuilder.AddSegment(textView, segment);
            var geometry = geoBuilder.CreateGeometry();

            if (geometry != null)
            {
                drawingContext.DrawGeometry(
                    isCurrent ? CurrentMatchBrush : MatchBrush,
                    null,
                    geometry);
            }
        }
    }
}
