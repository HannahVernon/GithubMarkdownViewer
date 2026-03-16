# GitHub Markdown Viewer

A cross-platform **.NET 9** desktop application for viewing and editing Markdown files with **live preview** and full **GitHub Flavored Markdown (GFM)** support.

Built with [Avalonia UI](https://avaloniaui.net/) and [Markdig](https://github.com/xoofx/markdig).

## Features

- **Live split-pane preview** — edit markdown on the left, see rendered output on the right
- **GitHub Flavored Markdown** — tables, task lists, strikethrough, autolinks, fenced code blocks, and more
- **File operations** — New, Open, Save, Save As with standard keyboard shortcuts
- **Export to HTML** — generate standalone HTML with GitHub-style CSS
- **View modes** — Split view, Editor only, or Preview only
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
# Clone and build
cd github-markdown-viewer
dotnet build

# Run the app
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

## Project Structure

```
GithubMarkdownViewer/
├── Program.cs                          # Entry point
├── App.axaml(.cs)                      # Application setup
├── Views/
│   └── MainWindow.axaml(.cs)           # Main window UI + file dialogs
├── ViewModels/
│   ├── ViewModelBase.cs                # MVVM base class
│   └── MainWindowViewModel.cs          # App logic & commands
└── Services/
    └── MarkdownService.cs              # Markdig GFM pipeline + HTML export
```

## Technology Stack

- **.NET 9** — latest cross-platform runtime
- **Avalonia UI 11** — cross-platform XAML UI framework
- **Markdig** — extensible Markdown processor with GFM pipeline
- **Markdown.Avalonia** — native Avalonia markdown rendering
- **CommunityToolkit.Mvvm** — source-generated MVVM pattern

## License

MIT
