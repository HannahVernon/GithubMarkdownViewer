# GitHub Markdown Viewer

A cross-platform **.NET 9** desktop application for viewing and editing Markdown files with **live preview** and full **GitHub Flavored Markdown (GFM)** support.

Built with [Avalonia UI](https://avaloniaui.net/) and [Markdig](https://github.com/xoofx/markdig).

## Features

### Editing & Preview
- **Live split-pane preview** — edit markdown on the left, see rendered output on the right, with synchronized scrolling (vertical and horizontal, bidirectional)
- **GitHub Flavored Markdown** — tables, task lists, strikethrough, autolinks, fenced code blocks, emoji, footnotes, and more
- **YAML front matter** — a leading `---` metadata block is hidden in the preview, matching GitHub's behavior
- **Dark & light theme support** — preview colors automatically adapt to the system theme using GitHub's color palettes
- **Word wrap toggle** — toggle text wrapping in the editor via View > Word Wrap; the rendered preview always wraps prose and table cells to the pane width, with wide code blocks scrolling sideways on their own (Shift+mouse wheel or touchpad)
- **Select across the whole preview** — drag to select text across paragraphs, lists, tables, and code blocks; double-click selects a word, triple-click a paragraph, and Ctrl+A selects everything; Ctrl+C or Edit > Copy copies plain text (blocks separated by a blank line, table cells by tabs); dragging past the top or bottom edge scrolls
- **Right-click menu** — right-click in the preview for Copy (when text is selected), Select All, and Copy paragraph, heading, code block, or cell contents for the block under the pointer; right-click a link for Copy URL and Open in Browser (enabled for `http` / `https` links only); right-clicking never changes the selection
- **Screen reader support** — the preview is exposed through UI Automation: headings with their levels, paragraphs, list items, table cells, and code blocks are readable elements, and links can be listed and activated by assistive technology
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
- **External change detection** — when the open file changes on disk, it reloads automatically and keeps your scroll and caret position; if you also have unsaved edits, the app asks before discarding them
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

### Building an installer (Windows)

```cmd
build.cmd
```

This publishes the win-x64 binaries and creates `installer\output\GithubMarkdownViewer-<version>-win-x64-setup.exe`. The version comes from `<Version>` in `GithubMarkdownViewer\GithubMarkdownViewer.csproj`. [Inno Setup 6](https://jrsoftware.org/isinfo.php) is optional; without it, the build creates a portable ZIP instead. Run `build.cmd all` to also publish the Linux and macOS binaries. See [installer/README.md](installer/README.md) for details.

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
| View     | Word Wrap       | Toggle text wrapping in the editor pane                  |
| Format   | Font...         | Choose font family and size                              |
| Help     | About...        | Application info, version, and GitHub repository link    |

A navigation toolbar with **◀ Back** and **▶ Forward** buttons appears below the menu bar for navigating between linked `.md` files.

## Project Structure

```
build.cmd                                   # One-command Windows build (calls build-all.ps1)
build-all.ps1                               # Publishes and packages installers
Directory.Build.props                       # NuGetAudit enforcement and security pins
global.json                                 # Test runner selection for the .NET 10 SDK
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
├── Preview/                                # Single-surface preview renderer
│   ├── DocumentModel.cs                    # Document model: blocks, rich text, style spans
│   ├── DocumentBuilder.cs                  # Markdig syntax tree → document model
│   ├── LayoutEngine.cs                     # Positions blocks into boxes (no UI dependency)
│   ├── LayoutTypes.cs                      # Boxes, text abstraction, layout result
│   ├── Selection.cs                        # Hit testing, selection ranges, copied text
│   ├── ContextMenuPlan.cs                  # Which right-click menu entries apply at a point
│   ├── AutomationModel.cs                  # Snapshot of blocks, headings, and links for screen readers
│   ├── Automation.cs                       # UI Automation peers that expose the preview
│   ├── AvaloniaTextProvider.cs             # Text layout and drawing via Avalonia TextLayout
│   ├── PreviewStyle.cs                     # Light and dark GitHub palettes
│   └── MarkdownPreviewControl.cs           # Draws the document; mouse, keyboard, scrolling
└── Services/
    ├── AppLogger.cs                        # File-based logger (%LOCALAPPDATA%)
    ├── FileAssociationService.cs           # Windows .md file extension association
    ├── MarkdownService.cs                  # Markdig GFM pipeline and sanitized HTML export
    └── SettingsService.cs                  # JSON settings persistence (%APPDATA%)
GithubMarkdownViewer.Tests/                 # xUnit tests for the document model, layout, and selection
installer/                                  # Installer scripts for Windows, Linux, and macOS
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
- **xUnit v3** — unit tests (test project only; not shipped)

## License

This project is licensed under the MIT License. Third-party dependency licenses (Avalonia, Markdig, CommunityToolkit.Mvvm, SkiaSharp, HarfBuzzSharp, MicroCom) are included in the [LICENSE](LICENSE) file.
