using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GithubMarkdownViewer.Models;

/// <summary>
/// Persisted application settings.
/// </summary>
public class AppSettings
{
    public const string DefaultFontFamily = "Cascadia Code";
    public const string FallbackFontFamily = "Consolas, Menlo, Monaco, Courier New, monospace";
    public const double DefaultFontSizePt = 10.0;
    public const double MinFontSizePt = 6.0;
    public const double MaxFontSizePt = 48.0;
    public const int MaxRecentFiles = 10;

    [JsonPropertyName("fontFamily")]
    public string FontFamilyName { get; set; } = DefaultFontFamily;

    [JsonPropertyName("fontSizePt")]
    public double FontSizePt { get; set; } = DefaultFontSizePt;

    [JsonPropertyName("lastOpenFilePath")]
    public string? LastOpenFilePath { get; set; }

    [JsonPropertyName("recentFiles")]
    public List<string> RecentFiles { get; set; } = new();

    [JsonPropertyName("showEditor")]
    public bool ShowEditor { get; set; } = true;

    [JsonPropertyName("showPreview")]
    public bool ShowPreview { get; set; } = true;

    [JsonPropertyName("wordWrap")]
    public bool WordWrap { get; set; } = true;

    [JsonPropertyName("declinedFileAssociation")]
    public bool DeclinedFileAssociation { get; set; } = false;

    // ── Window state ──────────────────────────────────────────────────

    [JsonPropertyName("windowX")]
    public double? WindowX { get; set; }

    [JsonPropertyName("windowY")]
    public double? WindowY { get; set; }

    [JsonPropertyName("windowWidth")]
    public double? WindowWidth { get; set; }

    [JsonPropertyName("windowHeight")]
    public double? WindowHeight { get; set; }

    [JsonPropertyName("windowState")]
    public string? WindowState { get; set; }

    /// <summary>
    /// Converts point size to Avalonia device-independent pixels (96 dpi).
    /// </summary>
    [JsonIgnore]
    public double FontSizePx => FontSizePt * 96.0 / 72.0;
}
