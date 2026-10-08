using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace GithubMarkdownViewer.Preview;

public enum TextKind
{
    Body,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    Heading5,
    Heading6,
    Code,
    Html,
}

/// <param name="Muted">Use the quote text colour (text inside a block quote).</param>
/// <param name="Strong">Use a heavier weight (table header cells).</param>
public readonly record struct TextRole(TextKind Kind, bool Muted = false, bool Strong = false);

public enum TextAlign
{
    Left,
    Center,
    Right,
}

/// <summary>A laid-out run of text that can be measured, hit-tested, and drawn.</summary>
public interface IPreviewText : IDisposable
{
    /// <summary>Width of the widest line.</summary>
    double Width { get; }

    double Height { get; }

    /// <summary>Number of characters in the text.</summary>
    int Length { get; }

    /// <summary>Maps a point (relative to the text origin) to a caret index from 0 to <see cref="Length"/>.</summary>
    int HitTest(Point point);

    /// <summary>Rectangles (relative to the text origin) covering a character range, one per wrapped line.</summary>
    IReadOnlyList<Rect> GetRangeRects(int start, int length);

    void Draw(DrawingContext context, Point origin);
}

/// <summary>Creates laid-out text. A fake implementation lets layout logic run in tests without Avalonia.</summary>
public interface ITextProvider
{
    /// <param name="maxWidth">Wrap width. Use <see cref="double.PositiveInfinity"/> for no wrapping.</param>
    IPreviewText Create(RichText text, TextRole role, double maxWidth, TextAlign align = TextAlign.Left);
}

public enum PaletteColor
{
    None,
    CodeBackground,
    CodeBorder,
    QuoteBorder,
    HrBackground,
    TableHeaderBackground,
    TableBorder,
    TableAltRowBackground,
}

public abstract class LayoutBox
{
    /// <summary>Position in content coordinates (before the control's padding).</summary>
    public Rect Bounds { get; set; }
}

public sealed class FillBox : LayoutBox
{
    public PaletteColor Fill { get; init; }
    public PaletteColor Stroke { get; init; }
    public double CornerRadius { get; init; }
}

/// <summary>What a text box represents, for assistive technology.</summary>
public enum BlockKind
{
    Text,
    Heading,
    Code,
    TableCell,
}

public sealed class TextLayoutBox : LayoutBox
{
    public required IPreviewText Text { get; init; }
    public required RichText Source { get; init; }

    /// <summary>False for list markers, which are not part of the copied text.</summary>
    public bool Selectable { get; init; } = true;

    public BlockKind Kind { get; init; } = BlockKind.Text;

    /// <summary>1 to 6 for headings, otherwise 0.</summary>
    public int HeadingLevel { get; init; }

    /// <summary>Position in <see cref="LayoutResult.TextBoxes"/>. Set for selectable boxes only.</summary>
    public int Index { get; set; } = -1;

    /// <summary>Text inserted before this box when copying a range that includes the previous box.</summary>
    public string SeparatorBefore { get; init; } = "\n\n";

    /// <summary>The code block that scrolls this text sideways, if any.</summary>
    public CodeBlockBox? ScrollOwner { get; set; }
}

public sealed class CheckMarkBox : LayoutBox
{
    public bool Checked { get; init; }
}

/// <summary>A code block. It draws its own background and scrolls its text sideways when it is wider than the block.</summary>
public sealed class CodeBlockBox : LayoutBox
{
    public const double Padding = 16;

    public required int Id { get; init; }

    /// <summary>The code text at scroll offset zero.</summary>
    public required TextLayoutBox Content { get; init; }

    public double ViewportWidth => Math.Max(0, Bounds.Width - 2 * Padding);

    public double MaxScrollX => Math.Max(0, Content.Text.Width - ViewportWidth);
}

public sealed record BlockAnchor(int StartLine, int EndLine, double Y, double Height);

public sealed class LayoutResult : IDisposable
{
    public required double Height { get; init; }
    public required IReadOnlyList<LayoutBox> Boxes { get; init; }

    /// <summary>Selectable text boxes in document order.</summary>
    public required IReadOnlyList<TextLayoutBox> TextBoxes { get; init; }

    /// <summary>Top-level blocks with their source lines and vertical position.</summary>
    public required IReadOnlyList<BlockAnchor> Anchors { get; init; }

    /// <summary>Heading id to the Y position of the heading.</summary>
    public required IReadOnlyDictionary<string, double> HeadingPositions { get; init; }

    public void Dispose()
    {
        foreach (var box in Boxes)
        {
            switch (box)
            {
                case TextLayoutBox text:
                    text.Text.Dispose();
                    break;
                case CodeBlockBox code:
                    code.Content.Text.Dispose();
                    break;
            }
        }
    }
}
