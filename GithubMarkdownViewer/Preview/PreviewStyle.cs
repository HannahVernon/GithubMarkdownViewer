using Avalonia.Media;

namespace GithubMarkdownViewer.Preview;

public sealed record PreviewStyle(string FontFamilyName, double BaseFontSize, FontWeight BaseFontWeight, bool Dark);

/// <summary>GitHub colours for the light and dark themes.</summary>
public sealed class PreviewPalette
{
    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex)).ToImmutable();

    private static readonly PreviewPalette Light = new()
    {
        Foreground = Brush("#1f2328"),
        CodeForeground = Brush("#1f2328"),
        QuoteForeground = Brush("#656d76"),
        LinkForeground = Brush("#0969da"),
        InlineCodeBackground = Brush("#eff1f3"),
        CodeBackground = Brush("#f6f8fa"),
        CodeBorder = Brush("#d1d9e0"),
        QuoteBorder = Brush("#d1d9e0"),
        HrBackground = Brush("#d1d9e0"),
        TableHeaderBackground = Brush("#f6f8fa"),
        TableBorder = Brush("#d1d9e0"),
        TableAltRowBackground = Brush("#f6f8fa"),
        ScrollThumb = Brush("#8c959f"),
        SelectionBackground = Brush("#660969da"),
    };

    private static readonly PreviewPalette Dark = new()
    {
        Foreground = Brush("#e6edf3"),
        CodeForeground = Brush("#e6edf3"),
        QuoteForeground = Brush("#8b949e"),
        LinkForeground = Brush("#58a6ff"),
        InlineCodeBackground = Brush("#343941"),
        CodeBackground = Brush("#161b22"),
        CodeBorder = Brush("#30363d"),
        QuoteBorder = Brush("#3b434b"),
        HrBackground = Brush("#30363d"),
        TableHeaderBackground = Brush("#161b22"),
        TableBorder = Brush("#30363d"),
        TableAltRowBackground = Brush("#161b22"),
        ScrollThumb = Brush("#6e7681"),
        SelectionBackground = Brush("#664493f8"),
    };

    public static PreviewPalette For(bool dark) => dark ? Dark : Light;

    public IBrush Foreground { get; private init; } = Brushes.Black;
    public IBrush CodeForeground { get; private init; } = Brushes.Black;
    public IBrush QuoteForeground { get; private init; } = Brushes.Gray;
    public IBrush LinkForeground { get; private init; } = Brushes.Blue;
    public IBrush InlineCodeBackground { get; private init; } = Brushes.LightGray;
    public IBrush CodeBackground { get; private init; } = Brushes.LightGray;
    public IBrush CodeBorder { get; private init; } = Brushes.Gray;
    public IBrush QuoteBorder { get; private init; } = Brushes.Gray;
    public IBrush HrBackground { get; private init; } = Brushes.Gray;
    public IBrush TableHeaderBackground { get; private init; } = Brushes.LightGray;
    public IBrush TableBorder { get; private init; } = Brushes.Gray;
    public IBrush TableAltRowBackground { get; private init; } = Brushes.LightGray;
    public IBrush ScrollThumb { get; private init; } = Brushes.Gray;
    public IBrush SelectionBackground { get; private init; } = Brushes.LightBlue;

    public IBrush? Get(PaletteColor color) => color switch
    {
        PaletteColor.CodeBackground => CodeBackground,
        PaletteColor.CodeBorder => CodeBorder,
        PaletteColor.QuoteBorder => QuoteBorder,
        PaletteColor.HrBackground => HrBackground,
        PaletteColor.TableHeaderBackground => TableHeaderBackground,
        PaletteColor.TableBorder => TableBorder,
        PaletteColor.TableAltRowBackground => TableAltRowBackground,
        _ => null,
    };
}
