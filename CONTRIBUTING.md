# Contributing to GithubMarkdownViewer

Thank you for your interest in contributing to GithubMarkdownViewer!

## Code of Conduct

This project follows the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to uphold this code.

## Getting Started

### Prerequisites

- Windows, macOS, or Linux
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Visual Studio 2022, JetBrains Rider, or VS Code with C# Dev Kit

### Building

1. Clone the repository:

   ```
   git clone https://github.com/HannahVernon/GithubMarkdownViewer.git
   cd GithubMarkdownViewer
   ```

2. Build the solution:

   ```
   dotnet build
   ```

### Running

```
dotnet run --project GithubMarkdownViewer
```

## How to Contribute

### Reporting Bugs

Open a [bug report](https://github.com/HannahVernon/GithubMarkdownViewer/issues/new?template=bug_report.yml) with steps to reproduce, expected vs. actual behavior, and your OS.

### Suggesting Features

Open a [feature request](https://github.com/HannahVernon/GithubMarkdownViewer/issues/new?template=feature_request.yml) describing the problem and your proposed solution.

### Pull Requests

1. Fork the repository and create a branch from `main`:

   ```
   git switch -c feature/your-feature main
   ```

2. Make your changes, keeping commits focused.

3. Ensure the project builds with zero warnings:

   ```
   dotnet build
   ```

4. Push your branch and open a pull request targeting `main`.

## Branching Model

- `main` is the primary development and release branch.
- Create `feature/` or `fix/` branches from `main`.

## License

This project is licensed under the [MIT License](LICENSE). By contributing, you agree that your contributions will be licensed under the same terms.