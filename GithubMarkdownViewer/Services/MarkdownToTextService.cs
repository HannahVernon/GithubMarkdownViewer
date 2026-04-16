using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Tables;

namespace GithubMarkdownViewer.Services;

/// <summary>
/// Converts a Markdig AST into readable ASCII-art plain text.
/// Produces nicely formatted output suitable for pasting into
/// plain-text contexts (email, Teams, chat, etc.).
/// </summary>
public class MarkdownToTextService
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownToTextService(MarkdownPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public string Convert(string markdown)
    {
        var document = Markdig.Markdown.Parse(markdown, _pipeline);
        var sb = new StringBuilder();
        RenderBlocks(sb, document, indent: "");
        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    private void RenderBlocks(StringBuilder sb, ContainerBlock container, string indent)
    {
        foreach (var block in container)
            RenderBlock(sb, block, indent);
    }

    private void RenderBlock(StringBuilder sb, Block block, string indent)
    {
        switch (block)
        {
            case HeadingBlock heading:
                RenderHeading(sb, heading, indent);
                break;

            case ParagraphBlock paragraph:
                RenderParagraph(sb, paragraph, indent);
                break;

            case FencedCodeBlock fencedCode:
                RenderCodeBlock(sb, fencedCode, indent);
                break;

            case CodeBlock codeBlock:
                RenderCodeBlock(sb, codeBlock, indent);
                break;

            case QuoteBlock quote:
                RenderBlockquote(sb, quote, indent);
                break;

            case ListBlock list:
                RenderList(sb, list, indent);
                break;

            case ThematicBreakBlock:
                sb.Append(indent);
                sb.AppendLine(new string('-', 72));
                sb.AppendLine();
                break;

            case Table table:
                RenderTable(sb, table, indent);
                break;

            case HtmlBlock htmlBlock:
                var htmlText = htmlBlock.Lines.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(htmlText))
                {
                    foreach (var line in htmlText.Split('\n'))
                    {
                        sb.Append(indent);
                        sb.AppendLine(line.TrimEnd());
                    }
                    sb.AppendLine();
                }
                break;

            default:
                if (block is LeafBlock leaf && leaf.Inline != null)
                {
                    RenderParagraph(sb, leaf, indent);
                }
                else if (block is ContainerBlock container)
                {
                    RenderBlocks(sb, container, indent);
                }
                break;
        }
    }

    private void RenderHeading(StringBuilder sb, HeadingBlock heading, string indent)
    {
        var text = GetInlineText(heading.Inline);

        sb.Append(indent);
        sb.AppendLine(text);

        sb.Append(indent);
        var underlineChar = heading.Level <= 1 ? '=' : '-';
        sb.AppendLine(new string(underlineChar, Math.Min(text.Length, 72)));
        sb.AppendLine();
    }

    private void RenderParagraph(StringBuilder sb, LeafBlock leaf, string indent)
    {
        var text = GetInlineText(leaf.Inline);
        if (string.IsNullOrWhiteSpace(text)) return;

        foreach (var line in WordWrap(text, 78 - indent.Length))
        {
            sb.Append(indent);
            sb.AppendLine(line);
        }
        sb.AppendLine();
    }

    private void RenderCodeBlock(StringBuilder sb, LeafBlock codeBlock, string indent)
    {
        var code = codeBlock.Lines.ToString().TrimEnd();
        var lines = code.Split('\n');
        var maxLen = lines.Max(l => l.TrimEnd().Length);
        var boxWidth = Math.Max(maxLen + 4, 20);

        sb.Append(indent);
        sb.Append('+');
        sb.Append(new string('-', boxWidth - 2));
        sb.AppendLine("+");

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd();
            sb.Append(indent);
            sb.Append("| ");
            sb.Append(trimmed);
            sb.Append(new string(' ', boxWidth - 4 - trimmed.Length));
            sb.AppendLine(" |");
        }

        sb.Append(indent);
        sb.Append('+');
        sb.Append(new string('-', boxWidth - 2));
        sb.AppendLine("+");
        sb.AppendLine();
    }

    private void RenderBlockquote(StringBuilder sb, QuoteBlock quote, string indent)
    {
        var quoteIndent = indent + "  > ";
        RenderBlocks(sb, quote, quoteIndent);
    }

    private void RenderList(StringBuilder sb, ListBlock list, string indent)
    {
        int itemNumber = list.IsOrdered ? (list.OrderedStart != null
            ? int.TryParse(list.OrderedStart, out var s) ? s : 1
            : 1) : 0;

        foreach (var item in list)
        {
            if (item is not ListItemBlock listItem) continue;

            var isTask = false;
            var taskChecked = false;

            // Check for task list checkbox in the first paragraph's inlines
            if (listItem.Count > 0 && listItem[0] is ParagraphBlock firstPara && firstPara.Inline != null)
            {
                var firstInline = firstPara.Inline.FirstChild;
                if (firstInline is TaskList taskList)
                {
                    isTask = true;
                    taskChecked = taskList.Checked;
                }
            }

            string bullet;
            if (isTask)
                bullet = taskChecked ? "[x] " : "[ ] ";
            else if (list.IsOrdered)
                bullet = $"{itemNumber}. ";
            else
                bullet = "- ";

            var bulletIndent = indent + new string(' ', bullet.Length);

            bool firstBlock = true;
            foreach (var childBlock in listItem)
            {
                if (firstBlock)
                {
                    firstBlock = false;
                    // Render first block with the bullet prefix
                    var text = childBlock is LeafBlock leaf && leaf.Inline != null
                        ? GetInlineText(leaf.Inline)
                        : "";

                    // For task items, skip the checkbox inline
                    if (isTask && childBlock is ParagraphBlock para && para.Inline != null)
                        text = GetInlineText(para.Inline, skipTaskList: true);

                    var wrapped = WordWrap(text, 78 - bulletIndent.Length);
                    for (int i = 0; i < wrapped.Count; i++)
                    {
                        sb.Append(i == 0 ? indent + bullet : bulletIndent);
                        sb.AppendLine(wrapped[i]);
                    }
                }
                else
                {
                    RenderBlock(sb, childBlock, bulletIndent);
                }
            }
        }
        sb.AppendLine();

        if (list.IsOrdered) itemNumber++;
    }

    private void RenderTable(StringBuilder sb, Table table, string indent)
    {
        // First pass: collect all cell text and measure column widths
        var rows = new List<(bool isHeader, List<string> cells)>();
        var colCount = table.ColumnDefinitions?.Count ?? 1;
        var colWidths = new int[colCount];

        foreach (var rowObj in table)
        {
            if (rowObj is not TableRow row) continue;
            var cells = new List<string>();
            int colIdx = 0;

            foreach (var cellObj in row)
            {
                if (cellObj is not TableCell cell) continue;

                var cellSb = new StringBuilder();
                foreach (var childBlock in cell)
                {
                    if (childBlock is LeafBlock leaf && leaf.Inline != null)
                        cellSb.Append(GetInlineText(leaf.Inline));
                }

                var cellText = cellSb.ToString().Trim();
                cells.Add(cellText);

                if (colIdx < colCount)
                    colWidths[colIdx] = Math.Max(colWidths[colIdx], cellText.Length);
                colIdx++;
            }
            rows.Add((row.IsHeader, cells));
        }

        // Ensure minimum column width of 3
        for (int i = 0; i < colWidths.Length; i++)
            colWidths[i] = Math.Max(colWidths[i], 3);

        // Render the table
        var separator = BuildTableSeparator(colWidths, indent);

        sb.AppendLine(separator);
        foreach (var (isHeader, cells) in rows)
        {
            sb.Append(indent);
            sb.Append('|');
            for (int c = 0; c < colCount; c++)
            {
                var cellText = c < cells.Count ? cells[c] : "";
                sb.Append(' ');
                sb.Append(cellText.PadRight(colWidths[c]));
                sb.Append(" |");
            }
            sb.AppendLine();

            if (isHeader)
                sb.AppendLine(separator);
        }
        sb.AppendLine(separator);
        sb.AppendLine();
    }

    private static string BuildTableSeparator(int[] colWidths, string indent)
    {
        var sep = new StringBuilder();
        sep.Append(indent);
        sep.Append('+');
        foreach (var w in colWidths)
        {
            sep.Append(new string('-', w + 2));
            sep.Append('+');
        }
        return sep.ToString();
    }

    // ── Inline text extraction ──────────────────────────────────────

    private static string GetInlineText(ContainerInline? container, bool skipTaskList = false)
    {
        if (container == null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            if (skipTaskList && inline is TaskList)
                continue;
            AppendInlineText(sb, inline);
        }
        return sb.ToString().Trim();
    }

    private static void AppendInlineText(StringBuilder sb, Inline inline)
    {
        switch (inline)
        {
            case LiteralInline literal:
                sb.Append(literal.Content);
                break;

            case EmphasisInline emphasis:
            {
                var isBold = emphasis.DelimiterChar is '*' or '_' && emphasis.DelimiterCount >= 2;
                var marker = isBold ? "**" : "*";

                sb.Append(marker);
                foreach (var child in emphasis)
                    AppendInlineText(sb, child);
                sb.Append(marker);
                break;
            }

            case CodeInline code:
                sb.Append('`');
                sb.Append(code.Content);
                sb.Append('`');
                break;

            case LinkInline link:
            {
                var linkText = new StringBuilder();
                if (link.Any())
                {
                    foreach (var child in link)
                        AppendInlineText(linkText, child);
                }

                var text = linkText.ToString();
                var url = link.Url ?? "";

                if (link.IsImage)
                {
                    sb.Append($"[Image: {text}]");
                }
                else if (string.Equals(text, url, StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append(url);
                }
                else
                {
                    sb.Append(text);
                    sb.Append($" ({url})");
                }
                break;
            }

            case AutolinkInline autolink:
                sb.Append(autolink.Url);
                break;

            case LineBreakInline:
                sb.AppendLine();
                break;

            case HtmlInline html:
                // Strip HTML tags in text output
                break;

            case HtmlEntityInline entity:
                sb.Append(entity.Transcoded);
                break;

            case ContainerInline container:
                foreach (var child in container)
                    AppendInlineText(sb, child);
                break;

            case TaskList task:
                sb.Append(task.Checked ? "[x] " : "[ ] ");
                break;
        }
    }

    // ── Word wrapping ───────────────────────────────────────────────

    private static List<string> WordWrap(string text, int maxWidth)
    {
        maxWidth = Math.Max(maxWidth, 20);
        var lines = new List<string>();

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length <= maxWidth)
            {
                lines.Add(line);
                continue;
            }

            var remaining = line;
            while (remaining.Length > maxWidth)
            {
                var breakPos = remaining.LastIndexOf(' ', maxWidth);
                if (breakPos <= 0) breakPos = maxWidth;

                lines.Add(remaining[..breakPos].TrimEnd());
                remaining = remaining[breakPos..].TrimStart();
            }
            if (remaining.Length > 0)
                lines.Add(remaining);
        }

        return lines;
    }
}
