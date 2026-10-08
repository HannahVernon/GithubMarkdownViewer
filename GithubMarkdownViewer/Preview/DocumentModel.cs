using System;
using System.Collections.Generic;

namespace GithubMarkdownViewer.Preview;

[Flags]
public enum SpanFlags
{
    None = 0,
    Bold = 1,
    Italic = 2,
    Strike = 4,
    Code = 8,
    Link = 16,
    Muted = 32,
}

/// <summary>A styled range inside a <see cref="RichText"/>. Spans never overlap.</summary>
public sealed record TextSpan(int Start, int Length, SpanFlags Flags, string? Url)
{
    public int End => Start + Length;
}

/// <summary>Plain text plus non-overlapping style spans.</summary>
public sealed class RichText
{
    public static readonly RichText Empty = new("", Array.Empty<TextSpan>());

    public RichText(string text, IReadOnlyList<TextSpan> spans)
    {
        Text = text;
        Spans = spans;
    }

    public string Text { get; }
    public IReadOnlyList<TextSpan> Spans { get; }

    public static RichText Plain(string text) => new(text, Array.Empty<TextSpan>());

    /// <summary>Returns the link URL at a character index, or null when the index is not inside a link.</summary>
    public string? UrlAt(int index)
    {
        foreach (var span in Spans)
        {
            if (span.Url != null && index >= span.Start && index < span.End)
                return span.Url;
        }
        return null;
    }
}

public abstract class DocNode
{
    /// <summary>First source line (1-based).</summary>
    public int StartLine { get; set; }

    /// <summary>Last source line (1-based).</summary>
    public int EndLine { get; set; }
}

public sealed class HeadingNode : DocNode
{
    public int Level { get; init; }
    public string? Id { get; init; }
    public RichText Text { get; init; } = RichText.Empty;
}

public sealed class ParagraphNode : DocNode
{
    public RichText Text { get; init; } = RichText.Empty;
}

public sealed class CodeNode : DocNode
{
    public string Text { get; init; } = "";
}

public sealed class HtmlNode : DocNode
{
    public string Text { get; init; } = "";
}

public sealed class RuleNode : DocNode
{
}

public sealed class QuoteNode : DocNode
{
    public List<DocNode> Children { get; } = new();
}

public sealed class ListItemNode
{
    /// <summary>Null for a normal item; true or false for a task list item.</summary>
    public bool? TaskChecked { get; init; }
    public List<DocNode> Children { get; } = new();
}

public sealed class ListNode : DocNode
{
    public bool Ordered { get; init; }
    public int Start { get; init; } = 1;
    public bool Tight { get; init; }
    public List<ListItemNode> Items { get; } = new();
}

public enum CellAlign
{
    Left,
    Center,
    Right,
}

public sealed class TableCellNode
{
    public RichText Text { get; init; } = RichText.Empty;
    public CellAlign Align { get; init; }
}

public sealed class TableRowNode
{
    public bool IsHeader { get; init; }
    public List<TableCellNode> Cells { get; } = new();
}

public sealed class TableNode : DocNode
{
    public int Columns { get; init; }
    public List<TableRowNode> Rows { get; } = new();
}
