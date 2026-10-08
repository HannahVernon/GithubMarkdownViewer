# Installer Build Guide

This directory contains scripts to build installers and packages for all supported platforms.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Windows installer:** [Inno Setup 6](https://jrsoftware.org/isinfo.php) (optional — falls back to portable ZIP)
- **Linux .deb:** `dpkg-deb` (included in most Debian/Ubuntu systems)
- **Linux .rpm:** `rpm-build` (`sudo dnf install rpm-build` or `sudo apt install rpm`)
- **macOS .dmg:** `hdiutil` (included in macOS — falls back to `.tar.gz` on other platforms)

## Quick Start (Windows)

```cmd
:: From the repo root: Windows installer only (fast)
build.cmd

:: Or publish every platform
build.cmd all
```

`build.cmd` runs `build-all.ps1` in the repo root. It reads the version from `<Version>` in `GithubMarkdownViewer\GithubMarkdownViewer.csproj` and uses it in the installer and archive file names.

`build.cmd` (win-x64 only) will:
1. Publish a self-contained Windows binary
2. Build the Windows installer (if Inno Setup is installed) or a portable ZIP

`build.cmd all` will also:
1. Publish self-contained binaries for Linux and macOS
2. Create a portable Linux `.tar.gz`
3. Print instructions for building Linux `.deb`/`.rpm` and macOS `.dmg`

## Platform-Specific Builds

### Windows Installer (.exe)

```powershell
# Publish Windows binaries
.\installer\publish.ps1 -Runtime win-x64

# Build with Inno Setup (GUI)
# Open installer\windows\setup.iss in Inno Setup and click Build
# (the version defaults to 1.0.0 unless you pass /DMyAppVersion)

# Or from command line (build.cmd does this and passes the csproj version)
iscc /DMyAppVersion=1.1.0 installer\windows\setup.iss
```

The installer supports:
- Per-user install (no admin required)
- Optional desktop shortcut
- Optional `.md` file association
- Start Menu shortcuts
- Standard uninstaller

### Linux .deb Package

```bash
# Publish Linux binaries (on Windows or Linux)
pwsh installer/publish.ps1 -Runtime linux-x64
# Or: dotnet publish GithubMarkdownViewer -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true -o installer/publish/linux-x64

# Build .deb (on Linux)
bash installer/linux/build-deb.sh

# Install
sudo dpkg -i installer/output/github-markdown-viewer_<version>_amd64.deb
```

The version comes from the first argument if you pass one (`bash installer/linux/build-deb.sh 1.2.3`), otherwise from the `APP_VERSION` environment variable, otherwise from `<Version>` in the csproj. The `.rpm` and `.dmg` scripts work the same way.

### Linux .rpm Package

```bash
# Publish Linux binaries first (same as above)

# Build .rpm (on Linux with rpm-build)
bash installer/linux/build-rpm.sh

# Install
sudo rpm -i installer/output/github-markdown-viewer-<version>-1*.rpm
```

### macOS .app Bundle + .dmg

```bash
# Publish macOS binaries
pwsh installer/publish.ps1 -Runtime osx-x64
# Or: dotnet publish GithubMarkdownViewer -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true -o installer/publish/osx-x64

# Build .dmg (on macOS)
bash installer/macos/build-dmg.sh
```

The `.dmg` includes a drag-and-drop install with an Applications folder shortcut.

#### Code Signing (Recommended for Distribution)

To sign the `.app` bundle for distribution outside the Mac App Store:

```bash
# Set your signing identity
export CODESIGN_IDENTITY="Developer ID Application: Your Name (TEAMID)"

# Build — signing happens automatically when CODESIGN_IDENTITY is set
bash installer/macos/build-dmg.sh

# Notarize for Gatekeeper
xcrun notarytool submit installer/output/GithubMarkdownViewer-<version>-osx-x64.dmg \
    --apple-id YOUR_APPLE_ID --team-id YOUR_TEAM_ID --wait
xcrun stapler staple installer/output/GithubMarkdownViewer-<version>-osx-x64.dmg
```

> **Note:** Without code signing, macOS users will see Gatekeeper warnings. The build script will print a reminder if `CODESIGN_IDENTITY` is not set.

## Output

All installer artifacts are written to `installer/output/`:

| File | Platform | Type |
|------|----------|------|
| `GithubMarkdownViewer-<version>-win-x64-setup.exe` | Windows | Inno Setup installer |
| `GithubMarkdownViewer-<version>-win-x64-portable.zip` | Windows | Portable (no install) |
| `GithubMarkdownViewer-<version>-linux-x64.tar.gz` | Linux | Portable tarball |
| `github-markdown-viewer_<version>_amd64.deb` | Linux | Debian package |
| `github-markdown-viewer-<version>-1.x86_64.rpm` | Linux | RPM package |
| `GithubMarkdownViewer-<version>-osx-x64.dmg` | macOS | Disk image |

`<version>` is the `<Version>` value in the csproj, unless you pass a version to the Linux or macOS scripts.

## Directory Structure

```
build.cmd                  # Repo root: entry point (calls build-all.ps1)
build-all.ps1              # Repo root: master build script (publish + installers)
installer/
├── publish.ps1            # Publishes self-contained binaries
├── README.md              # This file
├── windows/
│   └── setup.iss          # Inno Setup script
├── linux/
│   ├── build-deb.sh       # Debian package builder
│   └── build-rpm.sh       # RPM package builder
└── macos/
    └── build-dmg.sh       # macOS .app bundle + .dmg builder
```
