using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Tables;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Converts a Markdig AST into native Avalonia controls for the preview pane.
/// Supports GFM features: headings, paragraphs, bold/italic/strikethrough,
/// inline code, fenced code blocks, blockquotes, lists (including task lists),
/// tables, horizontal rules, and links.
/// </summary>
public class MarkdownToAvaloniaRenderer
{
    // GitHub Light palette
    private static readonly IBrush LightCodeBackground = new SolidColorBrush(Color.Parse("#f6f8fa"));
    private static readonly IBrush LightCodeBorder = new SolidColorBrush(Color.Parse("#d1d9e0"));
    private static readonly IBrush LightCodeForeground = new SolidColorBrush(Color.Parse("#1f2328"));
    private static readonly IBrush LightQuoteBorder = new SolidColorBrush(Color.Parse("#d1d9e0"));
    private static readonly IBrush LightQuoteForeground = new SolidColorBrush(Color.Parse("#656d76"));
    private static readonly IBrush LightLinkForeground = new SolidColorBrush(Color.Parse("#0969da"));
    private static readonly IBrush LightHrBackground = new SolidColorBrush(Color.Parse("#d1d9e0"));
    private static readonly IBrush LightTableHeaderBg = new SolidColorBrush(Color.Parse("#f6f8fa"));
    private static readonly IBrush LightTableBorder = new SolidColorBrush(Color.Parse("#d1d9e0"));
    private static readonly IBrush LightTableAltRowBg = new SolidColorBrush(Color.Parse("#f6f8fa"));
    private static readonly IBrush LightInlineCodeBg = new SolidColorBrush(Color.Parse("#eff1f3"));
    private static readonly IBrush LightDefaultForeground = new SolidColorBrush(Color.Parse("#1f2328"));

    // GitHub Dark palette
    private static readonly IBrush DarkCodeBackground = new SolidColorBrush(Color.Parse("#161b22"));
    private static readonly IBrush DarkCodeBorder = new SolidColorBrush(Color.Parse("#30363d"));
    private static readonly IBrush DarkCodeForeground = new SolidColorBrush(Color.Parse("#e6edf3"));
    private static readonly IBrush DarkQuoteBorder = new SolidColorBrush(Color.Parse("#3b434b"));
    private static readonly IBrush DarkQuoteForeground = new SolidColorBrush(Color.Parse("#8b949e"));
    private static readonly IBrush DarkLinkForeground = new SolidColorBrush(Color.Parse("#58a6ff"));
    private static readonly IBrush DarkHrBackground = new SolidColorBrush(Color.Parse("#30363d"));
    private static readonly IBrush DarkTableHeaderBg = new SolidColorBrush(Color.Parse("#161b22"));
    private static readonly IBrush DarkTableBorder = new SolidColorBrush(Color.Parse("#30363d"));
    private static readonly IBrush DarkTableAltRowBg = new SolidColorBrush(Color.Parse("#161b22"));
    private static readonly IBrush DarkInlineCodeBg = new SolidColorBrush(Color.Parse("#343941"));
    private static readonly IBrush DarkDefaultForeground = new SolidColorBrush(Color.Parse("#e6edf3"));

    // Active palette — set per render based on current theme
    private IBrush _codeBackground = LightCodeBackground;
    private IBrush _codeBorder = LightCodeBorder;
    private IBrush _codeForeground = LightCodeForeground;
    private IBrush _quoteBorder = LightQuoteBorder;
    private IBrush _quoteForeground = LightQuoteForeground;
    private IBrush _linkForeground = LightLinkForeground;
    private IBrush _hrBackground = LightHrBackground;
    private IBrush _tableHeaderBg = LightTableHeaderBg;
    private IBrush _tableBorder = LightTableBorder;
    private IBrush _tableAltRowBg = LightTableAltRowBg;
    private IBrush _inlineCodeBg = LightInlineCodeBg;
    private IBrush _defaultForeground = LightDefaultForeground;

    private readonly MarkdownPipeline _pipeline;

    // Current font settings — updated before each render
    private FontFamily _bodyFont = new("Inter, Segoe UI, sans-serif");
    private FontFamily _monoFont = new("Cascadia Code, Consolas, Menlo, monospace");
    private double _baseFontSize = 13.33; // 10pt in px
    private FontWeight _baseFontWeight = FontWeight.Regular;
    private TextWrapping _bodyTextWrapping = TextWrapping.Wrap;

    public MarkdownToAvaloniaRenderer(MarkdownPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    /// <summary>
    /// Raised when the user clicks a link in the rendered preview.
    /// The string argument is the link URL (may be relative or absolute).
    /// </summary>
    public event Action<string>? LinkClicked;

    /// <summary>
    /// Updates font settings used by subsequent Render calls.
    /// </summary>
    public void SetFont(string fontFamilyName, double baseFontSizePx, FontWeight baseFontWeight = default)
    {
        _bodyFont = new FontFamily($"{fontFamilyName}, Inter, Segoe UI, Noto Sans, Helvetica, Arial, sans-serif");
        _monoFont = new FontFamily($"{fontFamilyName}, Cascadia Code, Consolas, Menlo, Monaco, Courier New, monospace");
        _baseFontSize = baseFontSizePx;
        _baseFontWeight = baseFontWeight == default ? FontWeight.Regular : baseFontWeight;
    }

    /// <summary>
    /// Sets whether body text in the preview wraps or scrolls horizontally.
    /// Code blocks always use NoWrap regardless of this setting.
    /// </summary>
    public void SetWordWrap(bool wrap)
    {
        _bodyTextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
    }

    private void ApplyThemePalette()
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        if (isDark)
        {
            _codeBackground = DarkCodeBackground;
            _codeBorder = DarkCodeBorder;
            _codeForeground = DarkCodeForeground;
            _quoteBorder = DarkQuoteBorder;
            _quoteForeground = DarkQuoteForeground;
            _linkForeground = DarkLinkForeground;
            _hrBackground = DarkHrBackground;
            _tableHeaderBg = DarkTableHeaderBg;
            _tableBorder = DarkTableBorder;
            _tableAltRowBg = DarkTableAltRowBg;
            _inlineCodeBg = DarkInlineCodeBg;
            _defaultForeground = DarkDefaultForeground;
        }
        else
        {
            _codeBackground = LightCodeBackground;
            _codeBorder = LightCodeBorder;
            _codeForeground = LightCodeForeground;
            _quoteBorder = LightQuoteBorder;
            _quoteForeground = LightQuoteForeground;
            _linkForeground = LightLinkForeground;
            _hrBackground = LightHrBackground;
            _tableHeaderBg = LightTableHeaderBg;
            _tableBorder = LightTableBorder;
            _tableAltRowBg = LightTableAltRowBg;
            _inlineCodeBg = LightInlineCodeBg;
            _defaultForeground = LightDefaultForeground;
        }
    }

    /// <summary>
    /// Parses markdown text and returns a list of Avalonia controls to display.
    /// </summary>
    public IEnumerable<Control> Render(string markdown)
    {
        ApplyThemePalette();
        var document = Markdig.Markdown.Parse(markdown, _pipeline);
        return RenderBlocks(document);
    }

    private IEnumerable<Control> RenderBlocks(ContainerBlock container)
    {
        foreach (var block in container)
        {
            foreach (var control in RenderBlock(block))
                yield return control;
        }
    }

    private IEnumerable<Control> RenderBlock(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                yield return RenderHeading(heading);
                break;

            case ParagraphBlock paragraph:
                yield return RenderParagraph(paragraph);
                break;

            case FencedCodeBlock fencedCode:
                yield return RenderCodeBlock(fencedCode);
                break;

            case CodeBlock codeBlock:
                yield return RenderCodeBlock(codeBlock);
                break;

            case QuoteBlock quote:
                yield return RenderBlockquote(quote);
                break;

            case ListBlock list:
                yield return RenderList(list);
                break;

            case ThematicBreakBlock:
                yield return RenderHorizontalRule();
                break;

            case Markdig.Extensions.Tables.Table table:
                yield return RenderTable(table);
                break;

            case HtmlBlock htmlBlock:
                yield return RenderHtmlBlock(htmlBlock);
                break;

            default:
                // Fallback: render as plain text
                if (block is LeafBlock leaf && leaf.Inline != null)
                {
                    yield return RenderParagraph(leaf);
                    break;
                }
                if (block is ContainerBlock container)
                {
                    foreach (var child in RenderBlocks(container))
                        yield return child;
                }
                break;
        }
    }

    private Control RenderHeading(HeadingBlock heading)
    {
        var (scale, weight) = heading.Level switch
        {
            1 => (2.1, FontWeight.Bold),
            2 => (1.65, FontWeight.SemiBold),
            3 => (1.35, FontWeight.SemiBold),
            4 => (1.2, FontWeight.SemiBold),
            5 => (1.05, FontWeight.SemiBold),
            _ => (1.0, FontWeight.SemiBold),
        };

        var tb = new SelectableTextBlock
        {
            FontSize = _baseFontSize * scale,
            FontWeight = weight,
            FontFamily = _bodyFont,
            Foreground = _defaultForeground,
            TextWrapping = _bodyTextWrapping,
            Margin = new Thickness(0, 16, 0, 8),
        };
        SetInlines(tb, heading.Inline);

        if (heading.Level <= 2)
        {
            return new Border
            {
                BorderBrush = _codeBorder,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 6),
                Margin = new Thickness(0, 16, 0, 8),
                Child = tb,
            };
        }

        return tb;
    }

    private Control RenderParagraph(LeafBlock leaf)
    {
        var tb = new SelectableTextBlock
        {
            FontSize = _baseFontSize,
            FontFamily = _bodyFont,
            FontWeight = _baseFontWeight,
            Foreground = _defaultForeground,
            TextWrapping = _bodyTextWrapping,
            Margin = new Thickness(0, 0, 0, 12),
            LineHeight = _baseFontSize * 1.6,
        };
        SetInlines(tb, leaf.Inline);
        return tb;
    }

    private Control RenderCodeBlock(LeafBlock codeBlock)
    {
        var text = codeBlock.Lines.ToString().TrimEnd();
        return new Border
        {
            Background = _codeBackground,
            BorderBrush = _codeBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 12),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new SelectableTextBlock
            {
                Text = text,
                FontFamily = _monoFont,
                FontSize = _baseFontSize * 0.85,
                Foreground = _codeForeground,
                TextWrapping = TextWrapping.NoWrap,
                LineHeight = _baseFontSize * 1.45,
            }
        };
    }

    private Control RenderBlockquote(QuoteBlock quote)
    {
        var inner = new StackPanel { Spacing = 4 };
        foreach (var control in RenderBlocks(quote))
        {
            if (control is SelectableTextBlock tb)
                tb.Foreground = _quoteForeground;
            inner.Children.Add(control);
        }

        return new Border
        {
            BorderBrush = _quoteBorder,
            BorderThickness = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(16, 4, 0, 4),
            Margin = new Thickness(0, 0, 0, 12),
            Child = inner,
        };
    }

    private Control RenderList(ListBlock list)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 12) };
        int index = 1;

        foreach (var item in list.OfType<ListItemBlock>())
        {
            // Use DockPanel so the content fills remaining width and can wrap
            var row = new DockPanel();

            // Check for task list
            var firstParagraph = item.OfType<ParagraphBlock>().FirstOrDefault();
            var taskListInline = firstParagraph?.Inline?.FirstChild as TaskList;

            if (taskListInline != null)
            {
                var cb = new CheckBox
                {
                    IsChecked = taskListInline.Checked,
                    IsEnabled = false,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 0, 2, 0),
                };
                DockPanel.SetDock(cb, Dock.Left);
                row.Children.Add(cb);
            }
            else
            {
                var bullet = list.IsOrdered
                    ? $"{index}."
                    : "•";
                var bulletBlock = new TextBlock
                {
                    Text = bullet,
                    FontSize = _baseFontSize,
                    FontFamily = _bodyFont,
                    FontWeight = _baseFontWeight,
                    Foreground = _defaultForeground,
                    MinWidth = list.IsOrdered ? 24 : 16,
                    TextAlignment = TextAlignment.Right,
                    Margin = new Thickness(0, 0, 6, 0),
                    VerticalAlignment = VerticalAlignment.Top,
                };
                DockPanel.SetDock(bulletBlock, Dock.Left);
                row.Children.Add(bulletBlock);
            }

            var content = new StackPanel { Spacing = 2 };
            foreach (var childBlock in item)
            {
                foreach (var control in RenderBlock(childBlock))
                {
                    content.Children.Add(control);
                }
            }
            row.Children.Add(content);
            panel.Children.Add(row);
            index++;
        }

        return new Border
        {
            Padding = new Thickness(20, 0, 0, 0),
            Child = panel,
        };
    }

    private Control RenderHorizontalRule()
    {
        return new Border
        {
            Background = _hrBackground,
            Height = 3,
            CornerRadius = new CornerRadius(1.5),
            Margin = new Thickness(0, 16, 0, 16),
        };
    }

    private Control RenderTable(Markdig.Extensions.Tables.Table table)
    {
        var columns = table.ColumnDefinitions?.Count ?? 1;
        var grid = new Grid();

        for (int c = 0; c < columns; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        int rowIndex = 0;
        foreach (var rowObj in table)
        {
            if (rowObj is not TableRow row) continue;
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            bool isHeader = row.IsHeader;
            int colIndex = 0;

            foreach (var cellObj in row)
            {
                if (cellObj is not TableCell cell) continue;

                var cellContent = new StackPanel();
                foreach (var childBlock in cell)
                {
                    foreach (var control in RenderBlock(childBlock))
                    {
                        if (control is SelectableTextBlock stb)
                        {
                            stb.Margin = new Thickness(0);
                            if (isHeader) stb.FontWeight = FontWeight.SemiBold;
                        }
                        cellContent.Children.Add(control);
                    }
                }

                var cellBorder = new Border
                {
                    BorderBrush = _tableBorder,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(10, 6),
                    Background = isHeader ? _tableHeaderBg : (rowIndex % 2 == 0 ? Brushes.Transparent : _tableAltRowBg),
                    Child = cellContent,
                };

                Grid.SetRow(cellBorder, rowIndex);
                Grid.SetColumn(cellBorder, colIndex);
                grid.Children.Add(cellBorder);
                colIndex++;
            }
            rowIndex++;
        }

        // Wrap in a left-aligned container so each table sizes to its own content
        var wrapper = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 0, 0, 12),
            Child = grid,
        };
        return wrapper;
    }

    private Control RenderHtmlBlock(HtmlBlock htmlBlock)
    {
        var text = htmlBlock.Lines.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text)) return new Panel();
        return new SelectableTextBlock
        {
            Text = text,
            FontSize = _baseFontSize * 0.85,
            FontFamily = _monoFont,
            Foreground = _quoteForeground,
            TextWrapping = _bodyTextWrapping,
            Margin = new Thickness(0, 0, 0, 12),
        };
    }

    // ── Inline rendering ──────────────────────────────────────────────

    private void SetInlines(SelectableTextBlock target, ContainerInline? container)
    {
        if (container == null) return;
        target.Inlines ??= new Avalonia.Controls.Documents.InlineCollection();

        foreach (var inline in container)
        {
            foreach (var run in ConvertInline(inline))
                target.Inlines.Add(run);
        }
    }

    private IEnumerable<Avalonia.Controls.Documents.Inline> ConvertInline(Markdig.Syntax.Inlines.Inline inline)
    {
        switch (inline)
        {
            case TaskList task:
                // Handled at list-item level; skip here
                yield break;

            case LiteralInline literal:
                yield return new Avalonia.Controls.Documents.Run(literal.Content.ToString());
                break;

            case EmphasisInline emphasis:
            {
                var isBold = emphasis.DelimiterChar is '*' or '_' && emphasis.DelimiterCount >= 2;
                var isItalic = emphasis.DelimiterChar is '*' or '_' && emphasis.DelimiterCount == 1;
                var isStrikethrough = emphasis.DelimiterChar == '~';

                foreach (var child in emphasis)
                {
                    foreach (var run in ConvertInline(child))
                    {
                        if (isBold && run is Avalonia.Controls.Documents.Run r1)
                            r1.FontWeight = FontWeight.Bold;
                        if (isItalic && run is Avalonia.Controls.Documents.Run r2)
                            r2.FontStyle = FontStyle.Italic;
                        if (isStrikethrough && run is Avalonia.Controls.Documents.Run r3)
                            r3.TextDecorations = TextDecorations.Strikethrough;
                        yield return run;
                    }
                }
                break;
            }

            case CodeInline code:
                yield return new Avalonia.Controls.Documents.Run(code.Content)
                {
                    FontFamily = _monoFont,
                    FontSize = _baseFontSize * 0.85,
                    Background = _inlineCodeBg,
                    Foreground = _codeForeground,
                };
                break;

            case LinkInline link:
            {
                if (link.IsImage)
                {
                    // Images: just show alt text for now
                    var altText = link.FirstChild?.ToString() ?? link.Url ?? "";
                    yield return new Run($"[Image: {altText}]")
                    {
                        Foreground = _quoteForeground,
                        FontStyle = FontStyle.Italic,
                    };
                    break;
                }

                // Build display text from children
                var linkText = "";
                foreach (var child in link)
                {
                    if (child is LiteralInline lit)
                        linkText += lit.Content.ToString();
                    else if (child is CodeInline ci)
                        linkText += ci.Content;
                    else
                        linkText += child.ToString();
                }
                if (string.IsNullOrEmpty(linkText))
                    linkText = link.Url ?? "";

                var url = link.Url ?? "";
                yield return CreateClickableLink(linkText, url);
                break;
            }

            case AutolinkInline autolink:
                yield return CreateClickableLink(autolink.Url, autolink.Url);
                break;

            case LineBreakInline:
                yield return new Avalonia.Controls.Documents.LineBreak();
                break;

            case HtmlInline html:
                // Strip HTML tags; show raw text content if any
                var stripped = System.Text.RegularExpressions.Regex.Replace(html.Tag, "<[^>]+>", "");
                if (!string.IsNullOrEmpty(stripped))
                    yield return new Avalonia.Controls.Documents.Run(stripped);
                break;

            case HtmlEntityInline entity:
                yield return new Avalonia.Controls.Documents.Run(entity.Transcoded.ToString());
                break;

            case ContainerInline container:
                foreach (var child in container)
                {
                    foreach (var run in ConvertInline(child))
                        yield return run;
                }
                break;

            default:
                yield return new Run(inline.ToString() ?? "");
                break;
        }
    }

    /// <summary>
    /// Creates a clickable inline link that raises <see cref="LinkClicked"/> when clicked.
    /// Uses an InlineUIContainer wrapping a TextBlock styled as a hyperlink.
    /// A WeakReference to the renderer prevents detached controls from pinning
    /// this object (and its event subscribers) in memory after preview re-renders.
    /// </summary>
    private InlineUIContainer CreateClickableLink(string displayText, string url)
    {
        var tb = new TextBlock
        {
            Text = displayText,
            Foreground = _linkForeground,
            FontWeight = _baseFontWeight,
            TextDecorations = TextDecorations.Underline,
            Cursor = new Cursor(StandardCursorType.Hand),
            FontFamily = _bodyFont,
            FontSize = _baseFontSize,
            TextWrapping = _bodyTextWrapping,
        };
        ToolTip.SetTip(tb, url);

        var capturedUrl = url;
        var weakRenderer = new WeakReference<MarkdownToAvaloniaRenderer>(this);
        tb.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            if (weakRenderer.TryGetTarget(out var renderer))
                renderer.LinkClicked?.Invoke(capturedUrl);
        };

        return new InlineUIContainer { Child = tb };
    }
}
