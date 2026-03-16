# GitHub Markdown Viewer

A cross-platform **.NET 9** desktop application for viewing and editing Markdown files with **live preview** and full **GitHub Flavored Markdown (GFM)** support.

Built with [Avalonia UI](https://avaloniaui.net/) and [Markdig](https://github.com/xoofx/markdig).

## Features

- **Live split-pane preview** — edit markdown on the left, see rendered output on the right, with synchronized scrolling in both directions
- **GitHub Flavored Markdown** — tables, task lists, strikethrough, autolinks, fenced code blocks, and more
- **Dark & light theme support** — preview colors automatically adapt to the system theme using GitHub's color palettes
- **File operations** — New, Open, Save, Save As, Export HTML, with standard keyboard shortcuts
- **Recent files** — quick access to recently opened documents from the File menu
- **Auto-reopen** — automatically reopens the last document on startup
- **Configurable font** — choose any installed font and size via Format > Font, with Cascadia Code as the default
- **Unsaved changes protection** — prompts to Save / Don't Save / Cancel before closing or opening a new file
- **View modes** — Split view, Editor only, or Preview only — remembered across sessions
- **Window state persistence** — remembers window size, position, and maximized state
- **Cross-platform** — runs on Windows, macOS, and Linux

## GFM Extensions Supported

| Extension          | Status |
|--------------------|--------|
| Tables             | ✅     |
| Task lists         | ✅     |
| Strikethrough      | ✅     |
| Autolinks          | ✅     |
| Fenced code blocks | ✅     |
| Footnotes          | ✅     |
| Emoji              | ✅     |
| Definition lists   | ✅     |
| Abbreviations      | ✅     |
| Math (LaTeX)       | ✅     |

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

## Getting Started

```bash
# Clone the repo
git clone https://github.com/HannahVernon/GithubMarkdownViewer.git
cd GithubMarkdownViewer

# Build
dotnet build

# Run
dotnet run --project GithubMarkdownViewer
```

## Keyboard Shortcuts

| Shortcut          | Action          |
|-------------------|-----------------|
| `Ctrl+N`          | New file        |
| `Ctrl+O`          | Open file       |
| `Ctrl+S`          | Save            |
| `Ctrl+Shift+S`    | Save As         |
| `Ctrl+Shift+E`    | Export HTML     |
| `Alt+F4`          | Exit            |

## Menu Reference

| Menu     | Item            | Description                                      |
|----------|-----------------|--------------------------------------------------|
| File     | New             | Create a blank document                          |
| File     | Open...         | Open a markdown file                             |
| File     | Recent Files    | Submenu of recently opened documents             |
| File     | Save            | Save to the current file (or Save As if new)     |
| File     | Save As...      | Save to a new file                               |
| File     | Export HTML...  | Export as standalone HTML with GitHub-style CSS   |
| File     | Exit            | Close the application                            |
| View     | Split View      | Show both editor and preview panes               |
| View     | Editor Only     | Show only the editor pane                        |
| View     | Preview Only    | Show only the preview pane                       |
| Format   | Font...         | Choose font family and size                      |

## Project Structure

```
GithubMarkdownViewer/
├── Program.cs                              # Entry point with global exception handling
├── App.axaml(.cs)                          # Application setup and unhandled exception handlers
├── Models/
│   └── AppSettings.cs                      # Persisted settings model (font, window state, recents)
├── Views/
│   └── MainWindow.axaml(.cs)               # Main window UI, dialogs, scroll sync, layout
├── ViewModels/
│   ├── ViewModelBase.cs                    # MVVM base class
│   └── MainWindowViewModel.cs              # App logic, commands, file operations, settings
└── Services/
    ├── AppLogger.cs                        # File-based application logger
    ├── MarkdownService.cs                  # Markdig GFM pipeline and HTML export
    ├── MarkdownToAvaloniaRenderer.cs       # Custom Markdig AST → Avalonia controls renderer
    └── SettingsService.cs                  # JSON settings persistence
```

## Settings

Application settings are stored in `settings.json` next to the executable and include:

- Font family and size
- Last opened file path
- Recent files list (up to 10)
- View mode (split/editor/preview)
- Window position, size, and state

## Technology Stack

- **.NET 9** — latest cross-platform runtime
- **Avalonia UI 11** — cross-platform XAML UI framework
- **Markdig** — extensible Markdown processor with full GFM pipeline
- **CommunityToolkit.Mvvm** — source-generated MVVM pattern

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
