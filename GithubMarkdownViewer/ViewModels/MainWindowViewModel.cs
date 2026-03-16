using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GithubMarkdownViewer.Models;
using GithubMarkdownViewer.Services;

namespace GithubMarkdownViewer.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly MarkdownService _markdownService = new();

    /// <summary>Maximum file size (50 MB) to prevent out-of-memory on huge files.</summary>
    private const long MaxFileSizeBytes = 50 * 1024 * 1024;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(PreviewMarkdown))]
    private string _markdownText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    private string? _currentFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private bool _isModified;

    [ObservableProperty]
    private bool _showEditor = true;

    [ObservableProperty]
    private bool _showPreview = true;

    [ObservableProperty]
    private bool _wordWrap = true;

    [ObservableProperty]
    private string _statusText = "Ready";

    // ── Font settings ─────────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditorFontFamily))]
    private string _fontFamilyName = AppSettings.DefaultFontFamily;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FontSizePx))]
    private double _fontSizePt = AppSettings.DefaultFontSizePt;

    // ── Recent files ──────────────────────────────────────────────────

    public ObservableCollection<string> RecentFiles { get; } = new();

    /// <summary>
    /// FontFamily object for binding to the editor TextBox.
    /// Uses the chosen font with a monospace fallback chain.
    /// </summary>
    public FontFamily EditorFontFamily =>
        new($"{FontFamilyName}, {AppSettings.FallbackFontFamily}");

    /// <summary>
    /// Font size in Avalonia device-independent pixels (96 dpi).
    /// </summary>
    public double FontSizePx => FontSizePt * 96.0 / 72.0;

    public string WindowTitle
    {
        get
        {
            var fileLabel = CurrentFilePath ?? "Untitled";
            var modified = IsModified ? " •" : "";
            return $"{fileLabel}{modified} — GitHub Markdown Viewer";
        }
    }

    public bool HasFile => CurrentFilePath != null;

    /// <summary>
    /// Returns the markdown text for the preview pane.
    /// Markdown.Avalonia binds directly to this.
    /// </summary>
    public string PreviewMarkdown => MarkdownText;

    partial void OnMarkdownTextChanged(string value)
    {
        IsModified = true;
    }

    // Delegate for platform-specific file dialogs (set from code-behind)
    public Func<Task<string?>>? OpenFileDialog { get; set; }
    public Func<string?, Task<string?>>? SaveFileDialog { get; set; }
    public Func<string, string, Task>? ShowMessageDialog { get; set; }
    public Func<string, Task<bool>>? ConfirmDialog { get; set; }
    public Action? ExitApplication { get; set; }
    public Func<string, double, Task<(string fontFamily, double sizePt)?>>? FontPickerDialog { get; set; }

    /// <summary>
    /// File path passed via command-line argument (e.g. double-click from shell).
    /// </summary>
    public string? StartupFilePath { get; set; }

    [RelayCommand]
    private async Task ChangeFont()
    {
        if (FontPickerDialog == null) return;

        var result = await FontPickerDialog(FontFamilyName, FontSizePt);
        if (result == null) return;

        FontFamilyName = result.Value.fontFamily;
        FontSizePt = result.Value.sizePt;
        SaveSettings();
        StatusText = $"Font: {FontFamilyName}, {FontSizePt}pt";
    }

    public void LoadSettings()
    {
        var settings = SettingsService.Load();
        FontFamilyName = settings.FontFamilyName;
        FontSizePt = settings.FontSizePt;
        ShowEditor = settings.ShowEditor;
        ShowPreview = settings.ShowPreview;
        WordWrap = settings.WordWrap;
        DeclinedFileAssociation = settings.DeclinedFileAssociation;

        // Restore recent files list (only files that still exist)
        RecentFiles.Clear();
        foreach (var path in settings.RecentFiles.Where(File.Exists).Take(AppSettings.MaxRecentFiles))
            RecentFiles.Add(path);
    }

    /// <summary>
    /// Tries to open a startup file (command-line arg) or the last document from the previous session.
    /// Call after LoadSettings and initial UI setup.
    /// </summary>
    public async Task TryReopenLastFileAsync()
    {
        // Command-line file takes priority (e.g. double-click from shell)
        var fileToOpen = StartupFilePath;

        if (string.IsNullOrEmpty(fileToOpen) || !File.Exists(fileToOpen))
        {
            var settings = SettingsService.Load();
            fileToOpen = settings.LastOpenFilePath;
        }

        if (!string.IsNullOrEmpty(fileToOpen))
        {
            var content = await SafeReadFileAsync(fileToOpen);
            if (content != null)
            {
                MarkdownText = content;
                CurrentFilePath = fileToOpen;
                IsModified = false;
                AddToRecentFiles(fileToOpen);
                StatusText = $"Opened: {Path.GetFileName(fileToOpen)}";
            }
        }
    }

    private void SaveSettings()
    {
        SettingsService.Save(new AppSettings
        {
            FontFamilyName = FontFamilyName,
            FontSizePt = FontSizePt,
            LastOpenFilePath = CurrentFilePath,
            RecentFiles = RecentFiles.ToList(),
            ShowEditor = ShowEditor,
            ShowPreview = ShowPreview,
            WordWrap = WordWrap,
            DeclinedFileAssociation = DeclinedFileAssociation,
            WindowX = WindowX,
            WindowY = WindowY,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            WindowState = WindowState,
        });
    }

    // ── Window state (set by code-behind before save) ─────────────────

    [System.Text.Json.Serialization.JsonIgnore]
    public double? WindowX { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double? WindowY { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double? WindowWidth { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public double? WindowHeight { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string? WindowState { get; set; }

    // ── File association ─────────────────────────────────────────────
    [System.Text.Json.Serialization.JsonIgnore]
    public bool DeclinedFileAssociation { get; set; }

    public void AddToRecentFiles(string filePath)
    {
        // Remove if already present, then insert at top
        var existing = RecentFiles.FirstOrDefault(
            p => string.Equals(p, filePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            RecentFiles.Remove(existing);

        RecentFiles.Insert(0, filePath);

        // Cap the list
        while (RecentFiles.Count > AppSettings.MaxRecentFiles)
            RecentFiles.RemoveAt(RecentFiles.Count - 1);

        SaveSettings();
    }

    /// <summary>
    /// Saves settings including the current file path. Called on app exit.
    /// </summary>
    public void SaveSettingsOnExit() => SaveSettings();

    /// <summary>
    /// Safely reads a file after validating size and path.
    /// Returns null and shows a message dialog if the file is too large or the path is invalid.
    /// </summary>
    private async Task<string?> SafeReadFileAsync(string filePath)
    {
        try
        {
            var fullPath = Path.GetFullPath(filePath);

            // Block UNC paths to prevent NTLM hash leaks
            if (fullPath.StartsWith(@"\\", StringComparison.Ordinal))
            {
                if (ShowMessageDialog != null)
                    await ShowMessageDialog("Security Warning", "Cannot open files from network paths (UNC).");
                AppLogger.Warn("Blocked UNC path access attempt");
                return null;
            }

            var info = new FileInfo(fullPath);
            if (!info.Exists)
            {
                if (ShowMessageDialog != null)
                    await ShowMessageDialog("File Not Found", $"Could not find:\n{Path.GetFileName(filePath)}");
                return null;
            }

            if (info.Length > MaxFileSizeBytes)
            {
                if (ShowMessageDialog != null)
                    await ShowMessageDialog("File Too Large",
                        $"The file is {info.Length / (1024 * 1024):N0} MB, which exceeds the 50 MB limit.");
                return null;
            }

            return await File.ReadAllTextAsync(fullPath);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to read file", ex);
            if (ShowMessageDialog != null)
                await ShowMessageDialog("Error", $"Failed to read file:\n{ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears the recent files list and persists the change.
    /// </summary>
    public void ClearRecentFiles()
    {
        RecentFiles.Clear();
        SaveSettings();
    }

    [RelayCommand]
    private async Task NewFile()
    {
        if (!await HandleUnsavedChangesAsync()) return;

        MarkdownText = string.Empty;
        CurrentFilePath = null;
        IsModified = false;
        StatusText = "New file created";
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        if (OpenFileDialog == null) return;

        if (!await HandleUnsavedChangesAsync()) return;

        var path = await OpenFileDialog();
        if (path == null) return;

        var content = await SafeReadFileAsync(path);
        if (content != null)
        {
            MarkdownText = content;
            CurrentFilePath = path;
            IsModified = false;
            AddToRecentFiles(path);
            StatusText = $"Opened: {Path.GetFileName(path)}";
        }
    }

    [RelayCommand]
    private async Task OpenRecentFile(string path)
    {
        if (!await HandleUnsavedChangesAsync()) return;

        var content = await SafeReadFileAsync(path);
        if (content == null)
        {
            RecentFiles.Remove(path);
            SaveSettings();
            return;
        }

        MarkdownText = content;
        CurrentFilePath = path;
        IsModified = false;
        AddToRecentFiles(path);
        StatusText = $"Opened: {Path.GetFileName(path)}";
    }

    [RelayCommand]
    private async Task SaveFile()
    {
        if (CurrentFilePath == null)
        {
            await SaveFileAs();
            return;
        }

        try
        {
            await File.WriteAllTextAsync(CurrentFilePath, MarkdownText);
            IsModified = false;
            AddToRecentFiles(CurrentFilePath);
            StatusText = $"Saved: {Path.GetFileName(CurrentFilePath)}";
        }
        catch (Exception ex)
        {
            if (ShowMessageDialog != null)
                await ShowMessageDialog("Error", $"Failed to save file: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveFileAs()
    {
        if (SaveFileDialog == null) return;

        var path = await SaveFileDialog(CurrentFilePath);
        if (path == null) return;

        try
        {
            await File.WriteAllTextAsync(path, MarkdownText);
            CurrentFilePath = path;
            IsModified = false;
            AddToRecentFiles(path);
            StatusText = $"Saved: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            if (ShowMessageDialog != null)
                await ShowMessageDialog("Error", $"Failed to save file: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ExportHtml()
    {
        if (SaveFileDialog == null) return;

        var suggestedName = CurrentFilePath != null
            ? Path.ChangeExtension(CurrentFilePath, ".html")
            : null;
        var path = await SaveFileDialog(suggestedName);
        if (path == null) return;

        try
        {
            var html = _markdownService.ToFullHtml(MarkdownText);
            await File.WriteAllTextAsync(path, html);
            StatusText = $"Exported HTML: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            if (ShowMessageDialog != null)
                await ShowMessageDialog("Error", $"Failed to export HTML: {ex.Message}");
        }
    }

    // Delegate for unsaved-changes prompt: returns "save", "discard", or "cancel"
    public Func<Task<string>>? UnsavedChangesDialog { get; set; }

    /// <summary>
    /// Prompts the user if there are unsaved changes.
    /// Returns true if it's safe to proceed (user saved or discarded).
    /// Returns false if the user cancelled.
    /// </summary>
    public async Task<bool> HandleUnsavedChangesAsync()
    {
        if (!IsModified) return true;
        if (UnsavedChangesDialog == null) return true;

        var choice = await UnsavedChangesDialog();
        switch (choice)
        {
            case "save":
                await SaveFileAs();
                // If the user cancelled the Save As dialog, IsModified is still true
                return !IsModified;
            case "discard":
                return true;
            default: // "cancel"
                return false;
        }
    }

    [RelayCommand]
    private async Task ExitApp()
    {
        if (!await HandleUnsavedChangesAsync()) return;
        SaveSettings();
        ExitApplication?.Invoke();
    }

    [RelayCommand]
    private void ToggleEditor()
    {
        if (!ShowEditor && !ShowPreview) { ShowEditor = true; return; }
        ShowEditor = !ShowEditor;
        if (!ShowEditor && !ShowPreview) ShowPreview = true;
    }

    [RelayCommand]
    private void TogglePreview()
    {
        if (!ShowEditor && !ShowPreview) { ShowPreview = true; return; }
        ShowPreview = !ShowPreview;
        if (!ShowEditor && !ShowPreview) ShowEditor = true;
    }

    [RelayCommand]
    private void ViewSplit()
    {
        ShowEditor = true;
        ShowPreview = true;
    }

    [RelayCommand]
    private void ViewEditorOnly()
    {
        ShowEditor = true;
        ShowPreview = false;
    }

    [RelayCommand]
    private void ViewPreviewOnly()
    {
        ShowEditor = false;
        ShowPreview = true;
    }

    [RelayCommand]
    private void ToggleWordWrap()
    {
        WordWrap = !WordWrap;
        SaveSettings();
    }

    public void LoadSampleContent()
    {
        MarkdownText = """
            # Welcome to GitHub Markdown Viewer

            A cross-platform **.NET 9** markdown editor with **live preview**.

            ## Features

            - **GitHub Flavored Markdown** support
            - Split-pane editor and preview
            - File operations (New, Open, Save, Export HTML)
            - Cross-platform (Windows, macOS, Linux)

            ## GFM Examples

            ### Task Lists

            - [x] Create the project
            - [x] Add markdown rendering
            - [ ] Write documentation

            ### Tables

            | Feature        | Status  |
            |----------------|---------|
            | Headings       | ✅      |
            | Bold/Italic    | ✅      |
            | Links          | ✅      |
            | Code Blocks    | ✅      |
            | Tables         | ✅      |
            | Task Lists     | ✅      |
            | Strikethrough  | ✅      |

            ### Code

            Inline `code` and fenced code blocks:

            ```csharp
            // Hello from C#!
            Console.WriteLine("Hello, Markdown!");
            ```

            ```json
            {
              "name": "github-markdown-viewer",
              "version": "1.0.0"
            }
            ```

            ### Blockquotes

            > "The best way to predict the future is to invent it."
            > — Alan Kay

            ### Strikethrough

            This is ~~deleted~~ updated text.

            ### Links & Images

            Visit [GitHub](https://github.com) for more information.

            ---

            *Edit this text in the left pane to see the live preview update!*
            """;
        IsModified = false;
        StatusText = "Sample content loaded — start editing!";
    }
}
