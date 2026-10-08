using System;
using System.Collections.Generic;
using Avalonia;

namespace GithubMarkdownViewer.Preview;

public enum ContextAction
{
    OpenInBrowser,
    CopyUrl,
    CopySelection,
    CopyBlock,
    SelectAll,
}

/// <param name="Text">The URL or block text the action works on, when it has one.</param>
/// <param name="Group">Entries in different groups are separated by a divider in the menu.</param>
/// <param name="Gesture">Keyboard shortcut shown beside the entry, for information only.</param>
public sealed record ContextEntry(ContextAction Action, string Label, string? Text, int Group, bool Enabled = true,
    string? Gesture = null);

/// <summary>
/// Decides which entries the preview's right-click menu shows. It has no UI dependency, so the rules can
/// be tested without a window. The control turns the entries into menu items.
/// </summary>
public static class ContextMenuPlan
{
    private const int LinkGroup = 0;
    private const int CopyGroup = 1;
    private const int SelectGroup = 2;

    /// <param name="point">Where the user clicked, in content coordinates, or null when opened from the keyboard.</param>
    /// <param name="hasSelection">Whether any text is selected.</param>
    public static IReadOnlyList<ContextEntry> Build(LayoutResult layout, Point? point, bool hasSelection,
        Func<CodeBlockBox, double>? codeScroll = null)
    {
        var entries = new List<ContextEntry>();
        TextLayoutBox? block = null;

        if (point is { } clicked)
        {
            if (DocumentSelection.LinkAt(layout, clicked, codeScroll) is { } url)
            {
                // Only web addresses open in the browser, the same rule that applies to clicking a link.
                entries.Add(new ContextEntry(ContextAction.OpenInBrowser, "Open in Browser", url, LinkGroup, IsWebUrl(url)));
                entries.Add(new ContextEntry(ContextAction.CopyUrl, "Copy URL", url, LinkGroup));
            }
            block = DocumentSelection.BoxAt(layout, clicked);
        }

        if (hasSelection)
            entries.Add(new ContextEntry(ContextAction.CopySelection, "Copy", null, CopyGroup, Gesture: "Ctrl+C"));

        if (block != null && !string.IsNullOrWhiteSpace(block.Source.Text))
            entries.Add(new ContextEntry(ContextAction.CopyBlock, BlockLabel(block.Kind), block.Source.Text, CopyGroup));

        entries.Add(new ContextEntry(ContextAction.SelectAll, "Select All", null, SelectGroup, Gesture: "Ctrl+A"));
        return entries;
    }

    public static string BlockLabel(BlockKind kind) => kind switch
    {
        BlockKind.Heading => "Copy heading",
        BlockKind.Code => "Copy code block",
        BlockKind.TableCell => "Copy cell contents",
        _ => "Copy paragraph",
    };

    /// <summary>True for absolute http and https addresses, the only schemes the app opens in a browser.</summary>
    public static bool IsWebUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
