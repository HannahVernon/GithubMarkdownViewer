using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;

namespace GithubMarkdownViewer.Preview;

/// <summary>A link inside a block, with its bounds in content coordinates.</summary>
public sealed record LinkInfo(string Text, string Url, Rect Bounds);

/// <summary>One readable block of the document, as assistive technology sees it. Bounds are in content coordinates.</summary>
public sealed record BlockInfo(int Index, string Name, Rect Bounds, BlockKind Kind, int HeadingLevel,
    IReadOnlyList<LinkInfo> Links);

/// <summary>
/// Copies what a screen reader needs out of a layout. The result holds no reference to the layout, so it
/// stays valid after the layout is replaced and its text objects are disposed.
/// </summary>
public static class AutomationModel
{
    public static IReadOnlyList<BlockInfo> Build(LayoutResult layout)
    {
        var blocks = new List<BlockInfo>();
        foreach (var box in layout.TextBoxes)
        {
            // Empty table cells and blank lines add nothing to read.
            if (string.IsNullOrWhiteSpace(box.Source.Text)) continue;

            // A code block reports the whole block, not just the part of the text that is scrolled into view.
            var bounds = box.ScrollOwner?.Bounds ?? box.Bounds;
            var links = box.Kind == BlockKind.Code ? Array.Empty<LinkInfo>() : BuildLinks(box);
            blocks.Add(new BlockInfo(box.Index, box.Source.Text, bounds, box.Kind, box.HeadingLevel, links));
        }
        return blocks;
    }

    private static IReadOnlyList<LinkInfo> BuildLinks(TextLayoutBox box)
    {
        var links = new List<LinkInfo>();
        var spans = box.Source.Spans.Where(s => s.Url != null).ToList();

        for (var i = 0; i < spans.Count; i++)
        {
            // A link with bold or code inside is several spans with the same URL; read it as one link.
            var start = spans[i].Start;
            var end = spans[i].End;
            var url = spans[i].Url!;
            while (i + 1 < spans.Count && spans[i + 1].Url == url && spans[i + 1].Start == end)
            {
                end = spans[i + 1].End;
                i++;
            }

            var rects = box.Text.GetRangeRects(start, end - start);
            if (rects.Count == 0) continue;

            var union = rects.Aggregate((a, b) => a.Union(b)).Translate(new Vector(box.Bounds.X, box.Bounds.Y));
            links.Add(new LinkInfo(box.Source.Text.Substring(start, end - start), url, union));
        }
        return links;
    }
}
