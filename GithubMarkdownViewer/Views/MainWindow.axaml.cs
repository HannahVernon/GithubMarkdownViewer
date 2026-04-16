using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using GithubMarkdownViewer.Services;
using GithubMarkdownViewer.Models;
using GithubMarkdownViewer.ViewModels;

namespace GithubMarkdownViewer.Views;

public partial class MainWindow : Window
{
    private MarkdownToAvaloniaRenderer? _renderer;
    private DispatcherTimer? _previewTimer;
    private bool _previewDirty;
    private ScrollViewer? _editorScrollViewer;
    private bool _isSyncingScroll;

    // Navigation history for .md link traversal
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private Button? _backButton;
    private Button? _forwardButton;

    // Find-in-editor state
    private int _findMatchCount;
    private int _currentFindIndex = -1;
    private readonly List<int> _findMatchPositions = new();

    // File watcher state
    private FileSystemWatcher? _fileWatcher;
    private bool _isSelfSaving;
    private bool _reloadPromptPending;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            if (vm.IsModified)
            {
                e.Cancel = true;
                if (await vm.HandleUnsavedChangesAsync())
                {
                    vm.IsModified = false;
                    CaptureWindowState(vm);
                    vm.SaveSettingsOnExit();
                    CleanupEventHandlers(vm);
                    Close();
                }
                return;
            }
            CaptureWindowState(vm);
            vm.SaveSettingsOnExit();
            CleanupEventHandlers(vm);
        }
        base.OnClosing(e);
    }

    private void CleanupEventHandlers(MainWindowViewModel vm)
    {
        vm.PropertyChanged -= OnViewModelPropertyChanged;

        StopFileWatcher();

        if (_renderer != null)
            _renderer.LinkClicked -= OnRendererLinkClicked;

        if (_editorScrollViewer != null)
            _editorScrollViewer.PropertyChanged -= OnEditorScrollPropertyChanged;

        PreviewScrollViewer.PropertyChanged -= OnPreviewScrollPropertyChanged;

        RemoveHandler(PointerPressedEvent, OnNavigationPointerPressed);
    }

    private void CaptureWindowState(MainWindowViewModel vm)
    {
        vm.WindowState = WindowState.ToString();
        // Save the normal (restored) bounds, not maximized bounds
        if (WindowState == Avalonia.Controls.WindowState.Normal)
        {
            vm.WindowX = Position.X;
            vm.WindowY = Position.Y;
            vm.WindowWidth = Width;
            vm.WindowHeight = Height;
        }
    }

    private void RestoreWindowState(AppSettings settings)
    {
        if (settings.WindowWidth.HasValue && settings.WindowHeight.HasValue)
        {
            Width = settings.WindowWidth.Value;
            Height = settings.WindowHeight.Value;
        }
        if (settings.WindowX.HasValue && settings.WindowY.HasValue)
        {
            Position = new Avalonia.PixelPoint(
                (int)settings.WindowX.Value,
                (int)settings.WindowY.Value);
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        if (Enum.TryParse<Avalonia.Controls.WindowState>(settings.WindowState, out var state))
        {
            WindowState = state;
        }
    }

    private async Task InitContentAsync(MainWindowViewModel vm)
    {
        await vm.TryReopenLastFileAsync();
        if (vm.CurrentFilePath == null)
            vm.LoadSampleContent();
        UpdatePreview(vm.MarkdownText);

        // Check file association on Windows
        await CheckFileAssociationAsync(vm);
    }

    private async Task CheckFileAssociationAsync(MainWindowViewModel vm)
    {
        try
        {
            if (vm.DeclinedFileAssociation)
                return;

            if (FileAssociationService.IsAssociated())
                return;

            var result = await ConfirmAsync(
                "Markdown (.md) files are not currently associated with this application.\n\n" +
                "Would you like to associate .md files with GitHub Markdown Viewer so you can open them by double-clicking?");

            if (result)
            {
                var success = FileAssociationService.Associate();
                if (success)
                {
                    await ShowMessageAsync("File Association",
                        "The .md file extension has been associated with GitHub Markdown Viewer.");
                }
                else
                {
                    await ShowMessageAsync("File Association",
                        "Failed to set the file association. Check the application log for details.");
                }
            }
            else
            {
                vm.DeclinedFileAssociation = true;
                vm.SaveSettingsOnExit();
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("File association check failed", ex);
        }
    }

    // ── Recent files menu ─────────────────────────────────────────────

    private void OnRecentFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            RebuildRecentFilesMenu(vm);
    }

    private void RebuildRecentFilesMenu(MainWindowViewModel vm)
    {
        RecentFilesMenu.Items.Clear();

        if (vm.RecentFiles.Count == 0)
        {
            RecentFilesMenu.Items.Add(new MenuItem
            {
                Header = "(No recent files)",
                IsEnabled = false,
            });
            return;
        }

        // Show the first N items directly in the menu
        var menuCount = Math.Min(vm.RecentFiles.Count, AppSettings.MenuRecentFilesCount);
        for (int i = 0; i < menuCount; i++)
        {
            var path = vm.RecentFiles[i];
            var displayName = path;
            var item = new MenuItem
            {
                Header = displayName,
                Tag = path,
            };
            ToolTip.SetTip(item, path);
            item.Click += async (_, _) =>
            {
                if (item.Tag is string filePath)
                    await vm.OpenRecentFileCommand.ExecuteAsync(filePath);
            };
            RecentFilesMenu.Items.Add(item);
        }

        // "More..." item when there are additional files beyond the menu limit
        if (vm.RecentFiles.Count > AppSettings.MenuRecentFilesCount)
        {
            RecentFilesMenu.Items.Add(new Separator());
            var moreItem = new MenuItem
            {
                Header = $"More... ({vm.RecentFiles.Count - AppSettings.MenuRecentFilesCount} additional)",
            };
            moreItem.Click += async (_, _) => await ShowRecentFilesDialogAsync(vm);
            RecentFilesMenu.Items.Add(moreItem);
        }

        RecentFilesMenu.Items.Add(new Separator());
        var clearItem = new MenuItem { Header = "Clear Recent Files" };
        clearItem.Click += (_, _) =>
        {
            vm.RecentFiles.Clear();
            vm.ClearRecentFiles();
        };
        RecentFilesMenu.Items.Add(clearItem);
    }

    private async Task ShowRecentFilesDialogAsync(MainWindowViewModel vm)
    {
        var dialog = new Window
        {
            Title = "Recent Files",
            Width = 650,
            Height = 450,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = true,
            MinWidth = 400,
            MinHeight = 250,
        };

        var listBox = new ListBox
        {
            Margin = new Thickness(0),
            SelectionMode = Avalonia.Controls.SelectionMode.Single,
        };

        foreach (var path in vm.RecentFiles)
        {
            var displayPath = path;
            var item = new ListBoxItem
            {
                Content = displayPath,
                Tag = path,
                Padding = new Thickness(8, 4),
                FontSize = 13,
            };
            ToolTip.SetTip(item, path);
            listBox.Items.Add(item);
        }

        var openButton = new Button
        {
            Content = "Open",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Padding = new Thickness(24, 6),
            IsDefault = true,
            IsEnabled = false,
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Padding = new Thickness(24, 6),
            IsCancel = true,
        };

        listBox.SelectionChanged += (_, _) =>
        {
            openButton.IsEnabled = listBox.SelectedItem != null;
        };

        // Double-click to open
        listBox.DoubleTapped += (_, _) =>
        {
            if (listBox.SelectedItem is ListBoxItem selected && selected.Tag is string filePath)
                dialog.Close(filePath);
        };

        openButton.Click += (_, _) =>
        {
            if (listBox.SelectedItem is ListBoxItem selected && selected.Tag is string filePath)
                dialog.Close(filePath);
        };

        cancelButton.Click += (_, _) => dialog.Close(null);

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(12),
        };
        buttonPanel.Children.Add(openButton);
        buttonPanel.Children.Add(cancelButton);

        var layout = new DockPanel { Margin = new Thickness(0) };
        DockPanel.SetDock(buttonPanel, Avalonia.Controls.Dock.Bottom);
        layout.Children.Add(buttonPanel);
        layout.Children.Add(listBox);

        dialog.Content = layout;

        var result = await dialog.ShowDialog<string?>(this);
        if (result != null)
            await vm.OpenRecentFileCommand.ExecuteAsync(result);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is MainWindowViewModel vm)
        {
            try
            {
                _renderer = new MarkdownToAvaloniaRenderer(
                    new MarkdownService().Pipeline);
                _renderer.LinkClicked += OnRendererLinkClicked;

                vm.OpenFileDialog = OpenFileDialogAsync;
                vm.SaveFileDialog = SaveFileDialogAsync;
                vm.ShowMessageDialog = ShowMessageAsync;
                vm.ConfirmDialog = ConfirmAsync;
                vm.UnsavedChangesDialog = ShowUnsavedChangesDialogAsync;
                vm.ExitApplication = () => Close();
                vm.FontPickerDialog = ShowFontPickerAsync;
                vm.SettingsDialog = ShowSettingsDialogAsync;
                vm.SuppressFileWatcher = suppress => _isSelfSaving = suppress;
                vm.PropertyChanged += OnViewModelPropertyChanged;

                // Load persisted settings before rendering
                vm.LoadSettings();
                RestoreWindowState(SettingsService.Load());

                // Debounce preview updates at ~300ms to avoid lag while typing
                _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                _previewTimer.Tick += (_, _) =>
                {
                    _previewTimer.Stop();
                    if (_previewDirty)
                    {
                        _previewDirty = false;
                        UpdatePreview(vm.MarkdownText);
                    }
                };

                // Set up scroll sync — try immediately since template may already
                // be applied, and also subscribe as fallback for future applies.
                EditorTextBox.TemplateApplied += OnEditorTemplateApplied;
                // Deferred attempt: the visual tree is ready after the current layout pass
                Dispatcher.UIThread.Post(() => TryInitScrollSync(), DispatcherPriority.Loaded);

                // Wire up recent files menu
                vm.RecentFiles.CollectionChanged += OnRecentFilesChanged;
                RebuildRecentFilesMenu(vm);

                // Wire up About menu
                AboutMenuItem.Click += async (_, _) => await ShowAboutDialogAsync();

                // Wire up Edit > Find/Replace menu and find bar controls
                FindMenuItem.Click += (_, _) => ShowFindBar(showReplace: false);
                ReplaceMenuItem.Click += (_, _) => ShowFindBar(showReplace: true);
                FindCloseButton.Click += (_, _) => HideFindBar();
                FindNextButton.Click += (_, _) => FindNext();
                FindPrevButton.Click += (_, _) => FindPrevious();
                FindTextBox.TextChanged += (_, _) => PerformFind();
                FindTextBox.KeyDown += OnFindTextBoxKeyDown;
                ReplaceOneButton.Click += (_, _) => ReplaceOne();
                ReplaceAllButton.Click += (_, _) => ReplaceAll();

                // Wire up navigation buttons
                _backButton = this.FindControl<Button>("NavBackButton");
                _forwardButton = this.FindControl<Button>("NavForwardButton");
                if (_backButton != null)
                    _backButton.Click += async (_, _) => await NavigateBackAsync();
                if (_forwardButton != null)
                    _forwardButton.Click += async (_, _) => await NavigateForwardAsync();

                // Alt+Left / Alt+Right and mouse back/forward for navigation
                KeyDown += OnNavigationKeyDown;
                AddHandler(PointerPressedEvent, OnNavigationPointerPressed,
                    Avalonia.Interactivity.RoutingStrategies.Tunnel);

                // Try to reopen last document; fall back to sample content
                _ = InitContentAsync(vm);

                _renderer.SetFont(vm.FontFamilyName, vm.FontSizePx);
                _renderer.SetWordWrap(vm.WordWrap);
                ApplyWordWrapScrollBehavior(vm.WordWrap);
                ApplyTheme(vm.ThemeMode);
                UpdatePreview(vm.MarkdownText);
                UpdateLayout(vm);
                AppLogger.Info("MainWindow initialized successfully");
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to initialize MainWindow", ex);
            }
        }
    }

    // ── Scroll synchronization (bidirectional) ─────────────────────────

    private void OnEditorTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        TryInitScrollSync();
    }

    private void TryInitScrollSync()
    {
        if (_editorScrollViewer != null) return; // already initialized

        _editorScrollViewer = EditorTextBox
            .GetVisualDescendants()
            .OfType<ScrollViewer>()
            .FirstOrDefault();

        if (_editorScrollViewer != null)
        {
            _editorScrollViewer.PropertyChanged += OnEditorScrollPropertyChanged;
            PreviewScrollViewer.PropertyChanged += OnPreviewScrollPropertyChanged;
            AppLogger.Info("Scroll sync initialized");
        }
        else
        {
            AppLogger.Warn("Could not find editor ScrollViewer for scroll sync");
        }
    }

    private void OnEditorScrollPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty)
            SyncScroll(source: "editor");
    }

    private void OnPreviewScrollPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty)
            SyncScroll(source: "preview");
    }

    private void SyncScroll(string source)
    {
        if (_isSyncingScroll || _editorScrollViewer == null) return;

        try
        {
            _isSyncingScroll = true;

            ScrollViewer from, to;
            if (source == "editor")
            {
                from = _editorScrollViewer;
                to = PreviewScrollViewer;
            }
            else
            {
                from = PreviewScrollViewer;
                to = _editorScrollViewer;
            }

            double newX = to.Offset.X;
            double newY = to.Offset.Y;

            // Vertical sync
            var fromScrollableH = from.Extent.Height - from.Viewport.Height;
            var toScrollableH = to.Extent.Height - to.Viewport.Height;
            if (fromScrollableH > 0 && toScrollableH > 0)
                newY = (from.Offset.Y / fromScrollableH) * toScrollableH;

            // Horizontal sync
            var fromScrollableW = from.Extent.Width - from.Viewport.Width;
            var toScrollableW = to.Extent.Width - to.Viewport.Width;
            if (fromScrollableW > 0 && toScrollableW > 0)
                newX = (from.Offset.X / fromScrollableW) * toScrollableW;

            to.Offset = new Vector(newX, newY);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Error syncing scroll", ex);
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not MainWindowViewModel vm) return;

        try
        {
            if (e.PropertyName is nameof(MainWindowViewModel.ShowEditor)
                               or nameof(MainWindowViewModel.ShowPreview))
            {
                UpdateLayout(vm);
            }

            if (e.PropertyName is nameof(MainWindowViewModel.PreviewMarkdown)
                               or nameof(MainWindowViewModel.MarkdownText))
            {
                // Debounce: restart timer on each keystroke
                _previewDirty = true;
                _previewTimer?.Stop();
                _previewTimer?.Start();
            }

            // Font changes: update renderer and force re-render
            if (e.PropertyName is nameof(MainWindowViewModel.FontFamilyName)
                               or nameof(MainWindowViewModel.FontSizePt))
            {
                _renderer?.SetFont(vm.FontFamilyName, vm.FontSizePx);
                UpdatePreview(vm.MarkdownText);
            }

            // Word wrap toggle: update renderer and re-render
            if (e.PropertyName is nameof(MainWindowViewModel.WordWrap))
            {
                _renderer?.SetWordWrap(vm.WordWrap);
                ApplyWordWrapScrollBehavior(vm.WordWrap);
                UpdatePreview(vm.MarkdownText);
            }

            // Theme change: apply theme variant and re-render
            if (e.PropertyName is nameof(MainWindowViewModel.ThemeMode))
            {
                ApplyTheme(vm.ThemeMode);
                UpdatePreview(vm.MarkdownText);
            }

            // File path changed: restart file watcher
            if (e.PropertyName is nameof(MainWindowViewModel.CurrentFilePath))
            {
                StartFileWatcher(vm.CurrentFilePath);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Error in property change handler", ex);
        }
    }

    private void UpdatePreview(string markdown)
    {
        try
        {
            PreviewPanel.Children.Clear();
            if (_renderer == null || string.IsNullOrEmpty(markdown)) return;

            foreach (var control in _renderer.Render(markdown))
                PreviewPanel.Children.Add(control);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Error rendering markdown preview", ex);
            PreviewPanel.Children.Clear();
            PreviewPanel.Children.Add(new TextBlock
            {
                Text = "An error occurred while rendering the preview.",
                Foreground = Avalonia.Media.Brushes.Red,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Thickness(8),
            });
        }
    }

    // ── Link click handling ─────────────────────────────────────────

    private void OnRendererLinkClicked(string url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            // Check if this is a relative .md link (not a full URL)
            if (IsRelativeMarkdownLink(url))
            {
                _ = OpenRelativeMarkdownFileAsync(url);
                return;
            }

            // For absolute URLs, open in the default browser
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == "http" || uri.Scheme == "https"))
            {
                OpenUrlInBrowser(url);
            }
            else
            {
                AppLogger.Warn($"Blocked link with unsupported scheme: {uri?.Scheme ?? "unknown"}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to handle link click", ex);
        }
    }

    private static bool IsRelativeMarkdownLink(string url)
    {
        // Skip absolute URLs and anchors
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith('#'))
            return false;

        // Strip any fragment (e.g. "file.md#section")
        var pathPart = url.Split('#')[0];
        return pathPart.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    private async Task OpenRelativeMarkdownFileAsync(string relativeUrl)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // Strip fragment
        var pathPart = relativeUrl.Split('#')[0];

        // URL-decode (e.g. %20 → space)
        pathPart = Uri.UnescapeDataString(pathPart);

        // Resolve relative to the directory of the currently open file
        string? resolvedPath = null;
        string? baseDir = null;
        if (!string.IsNullOrEmpty(vm.CurrentFilePath))
        {
            baseDir = Path.GetDirectoryName(Path.GetFullPath(vm.CurrentFilePath));
            if (baseDir != null)
                resolvedPath = Path.GetFullPath(Path.Combine(baseDir, pathPart));
        }

        if (resolvedPath == null)
        {
            await ShowMessageAsync("Navigation Error",
                "Cannot resolve link — no file is currently open.");
            return;
        }

        // Block UNC paths to prevent NTLM hash leaks
        if (resolvedPath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            await ShowMessageAsync("Security Warning", "Cannot open files from network paths.");
            return;
        }

        // Path traversal protection: warn if navigating outside the current directory tree
        if (baseDir != null)
        {
            var normalizedBase = baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                 + Path.DirectorySeparatorChar;
            if (!resolvedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            {
                var proceed = await ConfirmAsync(
                    $"This link navigates outside the current directory to:\n{resolvedPath}\n\nOpen anyway?");
                if (!proceed) return;
            }
        }

        if (!File.Exists(resolvedPath))
        {
            await ShowMessageAsync("File Not Found",
                $"Could not find the linked file:\n{pathPart}");
            return;
        }

        // Check file size
        var fileInfo = new FileInfo(resolvedPath);
        if (fileInfo.Length > 50 * 1024 * 1024)
        {
            await ShowMessageAsync("File Too Large",
                $"The file is {fileInfo.Length / (1024 * 1024):N0} MB, which exceeds the 50 MB limit.");
            return;
        }

        // Prompt to save unsaved changes before navigating
        if (vm.IsModified)
        {
            var canProceed = await vm.HandleUnsavedChangesAsync();
            if (!canProceed) return;
        }

        // Open the linked .md file
        try
        {
            // Push current file onto back stack before navigating
            if (!string.IsNullOrEmpty(vm.CurrentFilePath))
            {
                _backStack.Push(vm.CurrentFilePath);
                _forwardStack.Clear();
                UpdateNavigationButtons();
            }

            var content = await File.ReadAllTextAsync(resolvedPath);
            vm.MarkdownText = content;
            vm.CurrentFilePath = resolvedPath;
            vm.IsModified = false;
            vm.AddToRecentFiles(resolvedPath);
            vm.StatusText = $"Opened: {Path.GetFileName(resolvedPath)}";
            UpdatePreview(content);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to open linked file", ex);
            await ShowMessageAsync("Error", "An error occurred while opening the linked file.");
        }
    }

    private static void OpenUrlInBrowser(string url)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else
            {
                // Use ArgumentList to avoid shell interpretation of URL contents
                var cmd = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open";
                var psi = new ProcessStartInfo(cmd) { UseShellExecute = false };
                psi.ArgumentList.Add(url);
                Process.Start(psi);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to open URL in browser", ex);
        }
    }

    // ── Back / Forward navigation ───────────────────────────────────

    private void UpdateNavigationButtons()
    {
        if (_backButton != null)
        {
            _backButton.IsEnabled = _backStack.Count > 0;
            ToolTip.SetTip(_backButton, _backStack.Count > 0
                ? $"Back to {Path.GetFileName(_backStack.Peek())} (Alt+Left)"
                : "Navigate back (Alt+Left)");
        }
        if (_forwardButton != null)
        {
            _forwardButton.IsEnabled = _forwardStack.Count > 0;
            ToolTip.SetTip(_forwardButton, _forwardStack.Count > 0
                ? $"Forward to {Path.GetFileName(_forwardStack.Peek())} (Alt+Right)"
                : "Navigate forward (Alt+Right)");
        }
    }

    private async Task NavigateBackAsync()
    {
        if (_backStack.Count == 0) return;
        if (DataContext is not MainWindowViewModel vm) return;

        if (vm.IsModified)
        {
            var canProceed = await vm.HandleUnsavedChangesAsync();
            if (!canProceed) return;
        }

        var target = _backStack.Pop();
        if (!string.IsNullOrEmpty(vm.CurrentFilePath))
            _forwardStack.Push(vm.CurrentFilePath);

        await NavigateToFileAsync(vm, target);
        UpdateNavigationButtons();
    }

    private async Task NavigateForwardAsync()
    {
        if (_forwardStack.Count == 0) return;
        if (DataContext is not MainWindowViewModel vm) return;

        if (vm.IsModified)
        {
            var canProceed = await vm.HandleUnsavedChangesAsync();
            if (!canProceed) return;
        }

        var target = _forwardStack.Pop();
        if (!string.IsNullOrEmpty(vm.CurrentFilePath))
            _backStack.Push(vm.CurrentFilePath);

        await NavigateToFileAsync(vm, target);
        UpdateNavigationButtons();
    }

    private async Task NavigateToFileAsync(MainWindowViewModel vm, string filePath)
    {
        // Block UNC paths
        if (filePath.StartsWith(@"\\", StringComparison.Ordinal))
        {
            await ShowMessageAsync("Security Warning", "Cannot open files from network paths.");
            return;
        }

        if (!File.Exists(filePath))
        {
            await ShowMessageAsync("File Not Found",
                $"Could not find:\n{Path.GetFileName(filePath)}");
            return;
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > 50 * 1024 * 1024)
        {
            await ShowMessageAsync("File Too Large",
                $"The file is {fileInfo.Length / (1024 * 1024):N0} MB, which exceeds the 50 MB limit.");
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            vm.MarkdownText = content;
            vm.CurrentFilePath = filePath;
            vm.IsModified = false;
            vm.AddToRecentFiles(filePath);
            vm.StatusText = $"Opened: {Path.GetFileName(filePath)}";
            UpdatePreview(content);
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to navigate to file", ex);
            await ShowMessageAsync("Error", "An error occurred while opening the file.");
        }
    }

    private void OnNavigationKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        // Ctrl+F: open find bar (find only)
        if (e.Key == Avalonia.Input.Key.F && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
        {
            e.Handled = true;
            ShowFindBar(showReplace: false);
            return;
        }

        // Ctrl+H: open find bar with replace
        if (e.Key == Avalonia.Input.Key.H && e.KeyModifiers == Avalonia.Input.KeyModifiers.Control)
        {
            e.Handled = true;
            ShowFindBar(showReplace: true);
            return;
        }

        // Escape: close find bar
        if (e.Key == Avalonia.Input.Key.Escape && FindBar.IsVisible)
        {
            e.Handled = true;
            HideFindBar();
            return;
        }

        // F3 / Shift+F3: next/previous match
        if (e.Key == Avalonia.Input.Key.F3 && FindBar.IsVisible)
        {
            e.Handled = true;
            if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
                FindPrevious();
            else
                FindNext();
            return;
        }

        // Alt+Left / Alt+Right for navigation
        if (e.KeyModifiers != Avalonia.Input.KeyModifiers.Alt) return;

        if (e.Key == Avalonia.Input.Key.Left && _backStack.Count > 0)
        {
            e.Handled = true;
            _ = NavigateBackAsync();
        }
        else if (e.Key == Avalonia.Input.Key.Right && _forwardStack.Count > 0)
        {
            e.Handled = true;
            _ = NavigateForwardAsync();
        }
    }

    private void OnNavigationPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsXButton1Pressed && _backStack.Count > 0)
        {
            e.Handled = true;
            _ = NavigateBackAsync();
        }
        else if (props.IsXButton2Pressed && _forwardStack.Count > 0)
        {
            e.Handled = true;
            _ = NavigateForwardAsync();
        }
    }

    // ── Find and Replace in editor ─────────────────────────────────

    private void ShowFindBar(bool showReplace)
    {
        FindBar.IsVisible = true;
        ReplaceRow.IsVisible = showReplace;
        FindTextBox.Focus();
        FindTextBox.SelectAll();

        // Seed the find box with the current editor selection
        if (!string.IsNullOrEmpty(EditorTextBox.SelectedText))
            FindTextBox.Text = EditorTextBox.SelectedText;
    }

    private void HideFindBar()
    {
        FindBar.IsVisible = false;
        _findMatchPositions.Clear();
        _findMatchCount = 0;
        _currentFindIndex = -1;
        FindStatusText.Text = "";
        EditorTextBox.Focus();
    }

    private void OnFindTextBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        if (e.Key == Avalonia.Input.Key.Enter)
        {
            e.Handled = true;
            if (e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift))
                FindPrevious();
            else
                FindNext();
        }
        else if (e.Key == Avalonia.Input.Key.Escape)
        {
            e.Handled = true;
            HideFindBar();
        }
    }

    private void PerformFind()
    {
        _findMatchPositions.Clear();
        _findMatchCount = 0;
        _currentFindIndex = -1;

        var searchText = FindTextBox.Text;
        var text = EditorTextBox.Text ?? string.Empty;

        if (string.IsNullOrEmpty(searchText) || string.IsNullOrEmpty(text))
        {
            FindStatusText.Text = "";
            return;
        }

        var pos = 0;
        while (pos < text.Length)
        {
            var idx = text.IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;
            _findMatchPositions.Add(idx);
            pos = idx + searchText.Length;
        }
        _findMatchCount = _findMatchPositions.Count;

        if (_findMatchCount > 0)
        {
            // Jump to the first match at or after the current caret position
            var caret = EditorTextBox.CaretIndex;
            _currentFindIndex = 0;
            for (int i = 0; i < _findMatchPositions.Count; i++)
            {
                if (_findMatchPositions[i] >= caret)
                {
                    _currentFindIndex = i;
                    break;
                }
            }
            SelectCurrentMatch(searchText.Length);
        }
        else
        {
            FindStatusText.Text = "No matches";
        }
    }

    private void FindNext()
    {
        if (_findMatchCount == 0) return;
        _currentFindIndex = (_currentFindIndex + 1) % _findMatchCount;
        SelectCurrentMatch(FindTextBox.Text?.Length ?? 0);
    }

    private void FindPrevious()
    {
        if (_findMatchCount == 0) return;
        _currentFindIndex = (_currentFindIndex - 1 + _findMatchCount) % _findMatchCount;
        SelectCurrentMatch(FindTextBox.Text?.Length ?? 0);
    }

    private void SelectCurrentMatch(int length)
    {
        if (_currentFindIndex < 0 || _currentFindIndex >= _findMatchPositions.Count) return;

        var pos = _findMatchPositions[_currentFindIndex];
        EditorTextBox.SelectionStart = pos;
        EditorTextBox.SelectionEnd = pos + length;
        EditorTextBox.CaretIndex = pos + length;
        FindStatusText.Text = $"{_currentFindIndex + 1} of {_findMatchCount}";
    }

    private void ReplaceOne()
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text ?? string.Empty;
        if (string.IsNullOrEmpty(searchText) || _findMatchCount == 0) return;

        // If the current selection matches the search text, replace it
        var selected = EditorTextBox.SelectedText;
        if (string.Equals(selected, searchText, StringComparison.OrdinalIgnoreCase))
        {
            var pos = EditorTextBox.SelectionStart;
            var text = EditorTextBox.Text ?? string.Empty;
            EditorTextBox.Text = string.Concat(
                text.AsSpan(0, pos),
                replaceText,
                text.AsSpan(pos + searchText.Length));
            EditorTextBox.CaretIndex = pos + replaceText.Length;

            // Re-index matches after replacement
            PerformFind();

            if (DataContext is MainWindowViewModel vm)
                vm.StatusText = "Replaced 1 occurrence";
        }
        else
        {
            // No active match selected — just find next
            FindNext();
        }
    }

    private void ReplaceAll()
    {
        var searchText = FindTextBox.Text;
        var replaceText = ReplaceTextBox.Text ?? string.Empty;
        if (string.IsNullOrEmpty(searchText)) return;

        var text = EditorTextBox.Text ?? string.Empty;
        var count = 0;
        var result = new System.Text.StringBuilder(text.Length);
        var pos = 0;

        while (pos < text.Length)
        {
            var idx = text.IndexOf(searchText, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                result.Append(text, pos, text.Length - pos);
                break;
            }
            result.Append(text, pos, idx - pos);
            result.Append(replaceText);
            pos = idx + searchText.Length;
            count++;
        }

        if (count > 0)
        {
            EditorTextBox.Text = result.ToString();
            PerformFind();

            if (DataContext is MainWindowViewModel vm)
                vm.StatusText = $"Replaced {count} occurrence{(count == 1 ? "" : "s")}";
        }
        else
        {
            FindStatusText.Text = "No matches";
        }
    }

    // ── File watcher ────────────────────────────────────────────────

    private void StartFileWatcher(string? filePath)
    {
        StopFileWatcher();

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return;

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            var fileName = Path.GetFileName(filePath);
            if (directory == null || fileName == null) return;

            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            _fileWatcher.Changed += OnWatchedFileChanged;
            AppLogger.Info($"File watcher started for: {fileName}");
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to start file watcher", ex);
        }
    }

    private void StopFileWatcher()
    {
        if (_fileWatcher == null) return;

        _fileWatcher.Changed -= OnWatchedFileChanged;
        _fileWatcher.EnableRaisingEvents = false;
        _fileWatcher.Dispose();
        _fileWatcher = null;
        _reloadPromptPending = false;
    }

    private void OnWatchedFileChanged(object sender, FileSystemEventArgs e)
    {
        // Ignore changes triggered by our own save operations
        if (_isSelfSaving) return;

        // FileSystemWatcher fires on a thread-pool thread and may fire
        // multiple times for a single save. Use a flag to debounce and
        // marshal to the UI thread.
        if (_reloadPromptPending) return;
        _reloadPromptPending = true;

        Dispatcher.UIThread.Post(() => _ = PromptReloadFileAsync(), DispatcherPriority.Background);
    }

    private async Task PromptReloadFileAsync()
    {
        try
        {
            if (DataContext is not MainWindowViewModel vm) return;
            if (string.IsNullOrEmpty(vm.CurrentFilePath)) return;

            var fileName = Path.GetFileName(vm.CurrentFilePath);
            var reload = await ConfirmAsync(
                $"The file \"{fileName}\" has been modified by another program.\n\nDo you want to reload it?");

            if (reload)
            {
                if (!File.Exists(vm.CurrentFilePath))
                {
                    await ShowMessageAsync("File Not Found",
                        $"The file no longer exists:\n{fileName}");
                    return;
                }

                var content = await File.ReadAllTextAsync(vm.CurrentFilePath);
                vm.MarkdownText = content;
                vm.IsModified = false;
                vm.StatusText = $"Reloaded: {fileName}";
                UpdatePreview(content);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to reload file", ex);
        }
        finally
        {
            _reloadPromptPending = false;
        }
    }

    private void UpdateLayout(MainWindowViewModel vm)
    {
        var cols = ContentGrid.ColumnDefinitions;
        if (vm.ShowEditor && vm.ShowPreview)
        {
            cols[0].Width = new GridLength(1, GridUnitType.Star);
            cols[1].Width = GridLength.Auto;
            cols[2].Width = new GridLength(1, GridUnitType.Star);
            PaneSplitter.IsVisible = true;
        }
        else if (vm.ShowEditor)
        {
            cols[0].Width = new GridLength(1, GridUnitType.Star);
            cols[1].Width = new GridLength(0);
            cols[2].Width = new GridLength(0);
            PaneSplitter.IsVisible = false;
        }
        else
        {
            cols[0].Width = new GridLength(0);
            cols[1].Width = new GridLength(0);
            cols[2].Width = new GridLength(1, GridUnitType.Star);
            PaneSplitter.IsVisible = false;
        }
    }

    private void ApplyWordWrapScrollBehavior(bool wordWrap)
    {
        // When wrapping, disable horizontal scroll so text has a width constraint to wrap against
        PreviewScrollViewer.HorizontalScrollBarVisibility =
            wordWrap ? Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled
                     : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
    }

    private void ApplyTheme(string themeMode)
    {
        if (Application.Current == null) return;

        Application.Current.RequestedThemeVariant = themeMode switch
        {
            "Light" => Avalonia.Styling.ThemeVariant.Light,
            "Dark" => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default, // "System" — follows OS
        };
    }

    private async Task ShowSettingsDialogAsync()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var dialog = new Window
        {
            Title = "Settings",
            Width = 500,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
        };

        // ── Theme selector ──────────────────────────────────────────
        var themeLabel = new TextBlock
        {
            Text = "Theme",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 6),
        };

        var themeOptions = new[] { "System", "Light", "Dark" };
        var themeCombo = new ComboBox
        {
            ItemsSource = themeOptions,
            SelectedItem = themeOptions.Contains(vm.ThemeMode) ? vm.ThemeMode : "System",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 16),
        };

        // ── Word Wrap ───────────────────────────────────────────────
        var wordWrapCheck = new CheckBox
        {
            Content = "Word Wrap",
            IsChecked = vm.WordWrap,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 16),
        };

        // ── Font section ────────────────────────────────────────────
        var fontLabel = new TextBlock
        {
            Text = "Font",
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 6),
        };

        var systemFonts = Avalonia.Media.FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fontList = new ListBox
        {
            ItemsSource = systemFonts,
            SelectedItem = systemFonts.Contains(vm.FontFamilyName) ? vm.FontFamilyName : systemFonts.FirstOrDefault(),
            Height = 180,
            Margin = new Thickness(0, 0, 0, 8),
        };

        var sizeLabel = new TextBlock
        {
            Text = "Size (pt):",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };

        var sizeUpDown = new NumericUpDown
        {
            Minimum = (decimal)Models.AppSettings.MinFontSizePt,
            Maximum = (decimal)Models.AppSettings.MaxFontSizePt,
            Increment = 0.5m,
            Value = (decimal)vm.FontSizePt,
            FormatString = "F1",
            Width = 100,
        };

        var fontPreview = new TextBlock
        {
            Text = "The quick brown fox jumps over the lazy dog",
            Margin = new Thickness(0, 8, 0, 0),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
        };

        // Live preview updates
        fontList.SelectionChanged += (_, _) =>
        {
            if (fontList.SelectedItem is string selectedFont)
            {
                fontPreview.FontFamily = new FontFamily($"{selectedFont}, {Models.AppSettings.FallbackFontFamily}");
            }
        };

        sizeUpDown.ValueChanged += (_, _) =>
        {
            if (sizeUpDown.Value.HasValue)
                fontPreview.FontSize = (double)sizeUpDown.Value.Value * 96.0 / 72.0;
        };

        // Set initial preview
        if (fontList.SelectedItem is string initialFont)
            fontPreview.FontFamily = new FontFamily($"{initialFont}, {Models.AppSettings.FallbackFontFamily}");
        fontPreview.FontSize = vm.FontSizePx;

        // Scroll to currently selected font
        if (fontList.SelectedItem != null)
        {
            Dispatcher.UIThread.Post(() =>
            {
                var idx = systemFonts.IndexOf((string)fontList.SelectedItem);
                if (idx >= 0)
                    fontList.ScrollIntoView(idx);
            }, DispatcherPriority.Loaded);
        }

        var sizePanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Children = { sizeLabel, sizeUpDown },
        };

        // ── Buttons ─────────────────────────────────────────────────
        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            IsDefault = true,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            IsCancel = true,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 0),
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        bool accepted = false;
        okButton.Click += (_, _) => { accepted = true; dialog.Close(); };
        cancelButton.Click += (_, _) => dialog.Close();

        // ── Layout ──────────────────────────────────────────────────
        var content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 0,
            Children =
            {
                themeLabel,
                themeCombo,
                wordWrapCheck,
                fontLabel,
                fontList,
                sizePanel,
                fontPreview,
                buttonPanel,
            },
        };

        dialog.Content = content;
        await dialog.ShowDialog(this);

        if (!accepted) return;

        // Apply changes
        var themeChanged = (themeCombo.SelectedItem as string ?? "System") != vm.ThemeMode;
        var fontChanged = (fontList.SelectedItem as string) != vm.FontFamilyName;
        var sizeChanged = sizeUpDown.Value.HasValue && (double)sizeUpDown.Value.Value != vm.FontSizePt;
        var wrapChanged = wordWrapCheck.IsChecked != vm.WordWrap;

        vm.ThemeMode = themeCombo.SelectedItem as string ?? "System";
        vm.WordWrap = wordWrapCheck.IsChecked ?? true;

        if (fontList.SelectedItem is string chosenFont)
            vm.FontFamilyName = chosenFont;
        if (sizeUpDown.Value.HasValue)
            vm.FontSizePt = (double)sizeUpDown.Value.Value;

        vm.SaveSettings();

        var changes = new List<string>();
        if (themeChanged) changes.Add($"Theme: {vm.ThemeMode}");
        if (fontChanged || sizeChanged) changes.Add($"Font: {vm.FontFamilyName}, {vm.FontSizePt}pt");
        if (wrapChanged) changes.Add($"Word Wrap: {(vm.WordWrap ? "On" : "Off")}");

        if (changes.Count > 0)
            vm.StatusText = $"Settings updated — {string.Join("; ", changes)}";
    }

    private static readonly FilePickerFileType MarkdownFileType = new("Markdown Files")
    {
        Patterns = new[] { "*.md", "*.markdown", "*.mdown", "*.mkd", "*.mkdn", "*.mdwn", "*.mdtxt", "*.mdtext", "*.txt" },
        MimeTypes = new[] { "text/markdown", "text/plain" }
    };

    private static readonly FilePickerFileType HtmlFileType = new("HTML Files")
    {
        Patterns = new[] { "*.html", "*.htm" },
        MimeTypes = new[] { "text/html" }
    };

    private static readonly FilePickerFileType TextFileType = new("Text Files")
    {
        Patterns = new[] { "*.txt" },
        MimeTypes = new[] { "text/plain" }
    };

    private async Task<string?> OpenFileDialogAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Markdown File",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType> { MarkdownFileType, FilePickerFileTypes.All }
        });

        return files.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string?> SaveFileDialogAsync(string? suggestedPath)
    {
        var suggestedName = suggestedPath != null
            ? System.IO.Path.GetFileName(suggestedPath)
            : "document.md";

        var isHtml = suggestedPath?.EndsWith(".html") == true || suggestedPath?.EndsWith(".htm") == true;
        var isTxt = suggestedPath?.EndsWith(".txt") == true;

        var defaultType = isHtml ? HtmlFileType : isTxt ? TextFileType : MarkdownFileType;
        var title = isHtml ? "Export as HTML" : isTxt ? "Export as Text" : "Save Markdown File";
        var defaultExt = isHtml ? "html" : isTxt ? "txt" : "md";

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = defaultExt,
            FileTypeChoices = new List<FilePickerFileType> { defaultType, FilePickerFileTypes.All }
        });

        return file?.Path.LocalPath;
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Width = 80 }
                }
            }
        };

        var button = ((StackPanel)dialog.Content).Children.OfType<Button>().First();
        button.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var result = false;
        var dialog = new Window
        {
            Title = "Confirm",
            Width = 420,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Spacing = 12,
                        Children =
                        {
                            new Button { Content = "Yes", Width = 80 },
                            new Button { Content = "No", Width = 80 }
                        }
                    }
                }
            }
        };

        var buttons = ((StackPanel)((StackPanel)dialog.Content).Children[1]).Children.OfType<Button>().ToList();
        buttons[0].Click += (_, _) => { result = true; dialog.Close(); };
        buttons[1].Click += (_, _) => { result = false; dialog.Close(); };

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<(string fontFamily, double sizePt)?> ShowFontPickerAsync(
        string currentFamily, double currentSizePt)
    {
        (string fontFamily, double sizePt)? result = null;

        // Gather system font names
        var systemFonts = Avalonia.Media.FontManager.Current.SystemFonts
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var fontList = new ListBox
        {
            ItemsSource = systemFonts,
            SelectedItem = systemFonts.Contains(currentFamily) ? currentFamily : systemFonts.FirstOrDefault(),
            Height = 260,
        };

        var sizeUpDown = new NumericUpDown
        {
            Minimum = (decimal)GithubMarkdownViewer.Models.AppSettings.MinFontSizePt,
            Maximum = (decimal)GithubMarkdownViewer.Models.AppSettings.MaxFontSizePt,
            Increment = 0.5m,
            Value = (decimal)currentSizePt,
            FormatString = "0.#",
            Width = 100,
        };

        var previewBlock = new TextBlock
        {
            Text = "AaBbCcDd 0123456789",
            FontSize = currentSizePt * 96.0 / 72.0,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };

        // Update preview when selection changes
        fontList.SelectionChanged += (_, _) =>
        {
            if (fontList.SelectedItem is string name)
                previewBlock.FontFamily = new Avalonia.Media.FontFamily(name);
        };
        sizeUpDown.ValueChanged += (_, _) =>
        {
            if (sizeUpDown.Value.HasValue)
                previewBlock.FontSize = (double)sizeUpDown.Value.Value * 96.0 / 72.0;
        };

        // Set initial preview font
        if (fontList.SelectedItem is string initial)
            previewBlock.FontFamily = new Avalonia.Media.FontFamily(initial);

        var okButton = new Button { Content = "OK", Width = 80, IsDefault = true };
        var cancelButton = new Button { Content = "Cancel", Width = 80, IsCancel = true };

        var dialog = new Window
        {
            Title = "Font Settings",
            Width = 400,
            Height = 480,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(16),
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Font Family:", FontWeight = Avalonia.Media.FontWeight.SemiBold },
                    fontList,
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "Size (pt):", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, FontWeight = Avalonia.Media.FontWeight.SemiBold },
                            sizeUpDown,
                        }
                    },
                    new Border
                    {
                        BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#d1d9e0")),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(12, 8),
                        Child = previewBlock,
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Margin = new Thickness(0, 8, 0, 0),
                        Children = { okButton, cancelButton }
                    }
                }
            }
        };

        okButton.Click += (_, _) =>
        {
            var selectedFont = fontList.SelectedItem as string ?? currentFamily;
            var selectedSize = sizeUpDown.Value.HasValue ? (double)sizeUpDown.Value.Value : currentSizePt;
            result = (selectedFont, selectedSize);
            dialog.Close();
        };
        cancelButton.Click += (_, _) => dialog.Close();

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task<string> ShowUnsavedChangesDialogAsync()
    {
        var result = "cancel";

        var saveBtn = new Button { Content = "Save", Width = 100, IsDefault = true };
        var discardBtn = new Button { Content = "Don't Save", Width = 100 };
        var cancelBtn = new Button { Content = "Cancel", Width = 100, IsCancel = true };

        var dialog = new Window
        {
            Title = "Unsaved Changes",
            Width = 440,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock
                    {
                        Text = "You have unsaved changes. Do you want to save them?",
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { saveBtn, discardBtn, cancelBtn }
                    }
                }
            }
        };

        saveBtn.Click += (_, _) => { result = "save"; dialog.Close(); };
        discardBtn.Click += (_, _) => { result = "discard"; dialog.Close(); };
        cancelBtn.Click += (_, _) => { result = "cancel"; dialog.Close(); };

        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowAboutDialogAsync()
    {
        const string repoUrl = "https://github.com/HannahVernon/GithubMarkdownViewer";

        Image? icon = null;
        try
        {
            var uri = new Uri("avares://GithubMarkdownViewer/Assets/app-icon.ico");
            using var stream = Avalonia.Platform.AssetLoader.Open(uri);
            var bitmap = new Avalonia.Media.Imaging.Bitmap(stream);
            icon = new Image { Source = bitmap, Width = 64, Height = 64, Margin = new Thickness(0, 0, 0, 12) };
        }
        catch
        {
            // Skip icon if loading fails
        }

        var linkText = new TextBlock
        {
            Text = repoUrl,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#0969da")),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            TextDecorations = Avalonia.Media.TextDecorations.Underline,
            Margin = new Thickness(0, 8, 0, 0),
        };
        linkText.PointerPressed += (_, _) =>
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(repoUrl) { UseShellExecute = true };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to open URL", ex);
            }
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            IsDefault = true,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };

        var content = new StackPanel
        {
            Margin = new Thickness(24),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        };
        if (icon != null) content.Children.Add(icon);
        content.Children.Add(new TextBlock
        {
            Text = "GitHub Markdown Viewer",
            FontSize = 18,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
        });
        content.Children.Add(new TextBlock
        {
            Text = "Version 1.0.0",
            FontSize = 12,
            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#656d76")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 12),
        });
        content.Children.Add(new TextBlock
        {
            Text = "A cross-platform .NET 9 markdown editor with live preview\nand full GitHub Flavored Markdown support.\n\nBuilt with Avalonia UI and Markdig.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            LineHeight = 20,
        });
        content.Children.Add(linkText);
        content.Children.Add(new Border { Height = 16 });
        content.Children.Add(okButton);

        var dialog = new Window
        {
            Title = "About GitHub Markdown Viewer",
            Width = 480,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = content,
        };

        okButton.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }
}