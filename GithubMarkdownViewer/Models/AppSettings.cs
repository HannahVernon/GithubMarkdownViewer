using System;
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
    public const int MaxRecentFiles = 100;
    public const int MenuRecentFilesCount = 10;

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

    [JsonPropertyName("showLineNumbers")]
    public bool ShowLineNumbers { get; set; } = true;

    [JsonPropertyName("themeMode")]
    public string ThemeMode { get; set; } = "System";

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

    /// <summary>
    /// Clamps all numeric values to valid ranges after deserialization.
    /// </summary>
    public void Sanitize()
    {
        FontSizePt = Math.Clamp(FontSizePt, MinFontSizePt, MaxFontSizePt);

        if (WindowWidth.HasValue)
            WindowWidth = Math.Clamp(WindowWidth.Value, 400, 7680);
        if (WindowHeight.HasValue)
            WindowHeight = Math.Clamp(WindowHeight.Value, 300, 4320);
        if (WindowX.HasValue)
            WindowX = Math.Clamp(WindowX.Value, -7680, 7680);
        if (WindowY.HasValue)
            WindowY = Math.Clamp(WindowY.Value, -4320, 4320);

        // Cap recent files list
        if (RecentFiles.Count > MaxRecentFiles)
            RecentFiles = RecentFiles.GetRange(0, MaxRecentFiles);

        // Truncate file paths to prevent oversized settings
        const int MaxPathLength = 32767;
        if (LastOpenFilePath != null && LastOpenFilePath.Length > MaxPathLength)
            LastOpenFilePath = null;
        for (int i = RecentFiles.Count - 1; i >= 0; i--)
        {
            if (RecentFiles[i] != null && RecentFiles[i].Length > MaxPathLength)
                RecentFiles.RemoveAt(i);
        }

        // Validate WindowState against known values
        var validStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Normal", "Minimized", "Maximized", "FullScreen" };
        if (WindowState != null && !validStates.Contains(WindowState))
            WindowState = "Normal";

        // Validate ThemeMode
        var validThemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "System", "Light", "Dark" };
        if (!validThemes.Contains(ThemeMode))
            ThemeMode = "System";

        // Sanitize font family — only allow ASCII-safe characters
        if (!string.IsNullOrEmpty(FontFamilyName) &&
            !System.Text.RegularExpressions.Regex.IsMatch(FontFamilyName, @"^[a-zA-Z0-9_ ,\-\.]+$"))
        {
            FontFamilyName = DefaultFontFamily;
        }
    }
}
