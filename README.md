# GitHub Markdown Viewer

A cross-platform **.NET 9** desktop application for viewing and editing Markdown files with **live preview** and full **GitHub Flavored Markdown (GFM)** support.

Built with [Avalonia UI](https://avaloniaui.net/) and [Markdig](https://github.com/xoofx/markdig).

## Features

### Editing & Preview
- **Live split-pane preview** — edit markdown on the left, see rendered output on the right, with synchronized scrolling (vertical and horizontal, bidirectional)
- **GitHub Flavored Markdown** — tables, task lists, strikethrough, autolinks, fenced code blocks, emoji, footnotes, and more
- **Dark & light theme support** — preview colors automatically adapt to the system theme using GitHub's color palettes
- **Word wrap toggle** — toggle text wrapping in the preview pane via View > Word Wrap, including proper wrapping of list items
- **View modes** — Split View, Editor Only, or Preview Only — remembered across sessions

### Clickable Links & Navigation
- **Clickable `.md` links** — relative markdown links in the preview pane open the linked file in the editor
- **Anchor links** — `#heading` links scroll to the matching heading within the current document; `file.md#heading` links navigate to the file and then scroll to the heading
- **GitHub-compatible heading IDs** — heading anchors are generated using GitHub's algorithm (preserves leading numbers, converts em dashes to hyphens)
- **Back / Forward navigation** — browser-style history with scroll-position restoration, supporting toolbar buttons, keyboard shortcuts (Alt+Left / Alt+Right), and mouse back/forward buttons; anchor jumps within the same file are also added to the history stack
- **External links** — `http` / `https` links open in the default browser
- **Link tooltips** — hover over any link to see the full URL

### File Operations
- **Full file operations** — New, Open, Save, Save As, Export HTML, with standard keyboard shortcuts
- **Recent files** — quick access to recently opened documents (showing parent directory context, with full path tooltips)
- **Auto-reopen** — automatically reopens the last document on startup
- **Command-line argument** — open a `.md` file by passing its path as an argument (supports double-click from shell)
- **Unsaved changes protection** — prompts to Save / Don't Save / Cancel before closing or opening a new file
- **HTML export** — exports as standalone HTML with GitHub-style CSS, with raw HTML sanitized to prevent XSS

### Customization & Persistence
- **Configurable font** — choose any installed font and size via Format > Font, with Cascadia Code 10pt as default
- **Window state persistence** — remembers window size, position, and maximized state across sessions
- **Settings stored in user profile** — settings saved to `%APPDATA%/GithubMarkdownViewer/`, logs to `%LOCALAPPDATA%/GithubMarkdownViewer/`

### Platform Integration
- **File association (Windows)** — optionally associate `.md` files with the app on first run for double-click opening
- **Cross-platform** — runs on Windows, macOS, and Linux
- **About dialog** — app info, version, and link to the GitHub repository

### Security
- **Path traversal protection** — warns when markdown links navigate outside the current directory
- **UNC path blocking** — rejects network paths to prevent NTLM credential leaks
- **File size limits** — rejects files larger than 50 MB to prevent out-of-memory crashes
- **HTML sanitization** — raw HTML in markdown is escaped in HTML exports to prevent script injection
- **No JavaScript execution** — the preview pane renders to native UI controls; embedded scripts are displayed as inert text

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
| Extra emphasis     | ✅     |
| Citations          | ✅     |
| Custom containers  | ✅     |
| Figures            | ✅     |
| Diagrams           | ✅     |

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

# Open a specific file
dotnet run --project GithubMarkdownViewer -- path/to/file.md
```

## Keyboard Shortcuts

| Shortcut          | Action              |
|-------------------|---------------------|
| `Ctrl+N`          | New file            |
| `Ctrl+O`          | Open file           |
| `Ctrl+S`          | Save                |
| `Ctrl+Shift+S`    | Save As             |
| `Ctrl+Shift+E`    | Export HTML          |
| `Alt+Left`        | Navigate back       |
| `Alt+Right`       | Navigate forward    |
| `Alt+F4`          | Exit                |

Mouse back/forward buttons also work for navigation.

## Menu Reference

| Menu     | Item            | Description                                              |
|----------|-----------------|----------------------------------------------------------|
| File     | New             | Create a blank document                                  |
| File     | Open...         | Open a markdown file                                     |
| File     | Recent Files    | Submenu of recently opened documents                     |
| File     | Save            | Save to the current file (or Save As if new)             |
| File     | Save As...      | Save to a new file                                       |
| File     | Export HTML...  | Export as standalone HTML with GitHub-style CSS           |
| File     | Exit            | Close the application                                    |
| View     | Split View      | Show both editor and preview panes                       |
| View     | Editor Only     | Show only the editor pane                                |
| View     | Preview Only    | Show only the preview pane                               |
| View     | Word Wrap       | Toggle text wrapping in the preview pane                 |
| Format   | Font...         | Choose font family and size                              |
| Help     | About...        | Application info, version, and GitHub repository link    |

A navigation toolbar with **◀ Back** and **▶ Forward** buttons appears below the menu bar for navigating between linked `.md` files.

## Project Structure

```
GithubMarkdownViewer/
├── Program.cs                              # Entry point with global exception handling
├── App.axaml(.cs)                          # Application setup, exception handlers, CLI args
├── Models/
│   └── AppSettings.cs                      # Persisted settings model with validation
├── Views/
│   └── MainWindow.axaml(.cs)               # Main window UI, dialogs, scroll sync, navigation
├── ViewModels/
│   ├── ViewModelBase.cs                    # MVVM base class
│   └── MainWindowViewModel.cs              # App logic, commands, file ops, safe file reading
└── Services/
    ├── AppLogger.cs                        # File-based logger (%LOCALAPPDATA%)
    ├── FileAssociationService.cs           # Windows .md file extension association
    ├── MarkdownService.cs                  # Markdig GFM pipeline and sanitized HTML export
    ├── MarkdownToAvaloniaRenderer.cs       # Custom Markdig AST → Avalonia controls renderer
    └── SettingsService.cs                  # JSON settings persistence (%APPDATA%)
```

## Settings

Application settings are stored in `%APPDATA%/GithubMarkdownViewer/settings.json` (with automatic migration from legacy locations) and include:

- Font family and size
- Last opened file path
- Recent files list (up to 10)
- View mode (split/editor/preview)
- Word wrap preference
- Window position, size, and state
- File association preference

## Technology Stack

- **.NET 9** — latest cross-platform runtime
- **Avalonia UI 11** — cross-platform XAML UI framework
- **Markdig** — extensible Markdown processor with full GFM pipeline
- **CommunityToolkit.Mvvm** — source-generated MVVM pattern

## License

This project is licensed under the MIT License. Third-party dependency licenses (Avalonia, Markdig, CommunityToolkit.Mvvm, SkiaSharp, HarfBuzzSharp, MicroCom) are included in the [LICENSE](LICENSE) file.
