# Installer Build Guide

This directory contains scripts to build installers and packages for all supported platforms.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Windows installer:** [Inno Setup 6](https://jrsoftware.org/isinfo.php) (optional — falls back to portable ZIP)
- **Linux .deb:** `dpkg-deb` (included in most Debian/Ubuntu systems)
- **Linux .rpm:** `rpm-build` (`sudo dnf install rpm-build` or `sudo apt install rpm`)
- **macOS .dmg:** `hdiutil` (included in macOS — falls back to `.tar.gz` on other platforms)

## Quick Start (Windows)

```powershell
# Build everything from the repo root
.\installer\build-all.ps1
```

This will:
1. Publish self-contained binaries for Windows, Linux, and macOS
2. Build the Windows installer (if Inno Setup is installed) or a portable ZIP
3. Create a portable Linux `.tar.gz`
4. Print instructions for building Linux `.deb`/`.rpm` and macOS `.dmg`

## Platform-Specific Builds

### Windows Installer (.exe)

```powershell
# Publish Windows binaries
.\installer\publish.ps1 -Runtime win-x64

# Build with Inno Setup (GUI)
# Open installer\windows\setup.iss in Inno Setup and click Build

# Or from command line
iscc installer\windows\setup.iss
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
sudo dpkg -i installer/output/github-markdown-viewer_1.0.0_amd64.deb
```

### Linux .rpm Package

```bash
# Publish Linux binaries first (same as above)

# Build .rpm (on Linux with rpm-build)
bash installer/linux/build-rpm.sh

# Install
sudo rpm -i installer/output/github-markdown-viewer-1.0.0-1*.rpm
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
xcrun notarytool submit installer/output/GithubMarkdownViewer-1.0.0-osx-x64.dmg \
    --apple-id YOUR_APPLE_ID --team-id YOUR_TEAM_ID --wait
xcrun stapler staple installer/output/GithubMarkdownViewer-1.0.0-osx-x64.dmg
```

> **Note:** Without code signing, macOS users will see Gatekeeper warnings. The build script will print a reminder if `CODESIGN_IDENTITY` is not set.

## Output

All installer artifacts are written to `installer/output/`:

| File | Platform | Type |
|------|----------|------|
| `GithubMarkdownViewer-1.0.0-win-x64-setup.exe` | Windows | Inno Setup installer |
| `GithubMarkdownViewer-1.0.0-win-x64-portable.zip` | Windows | Portable (no install) |
| `GithubMarkdownViewer-1.0.0-linux-x64.tar.gz` | Linux | Portable tarball |
| `github-markdown-viewer_1.0.0_amd64.deb` | Linux | Debian package |
| `github-markdown-viewer-1.0.0-1.x86_64.rpm` | Linux | RPM package |
| `GithubMarkdownViewer-1.0.0-osx-x64.dmg` | macOS | Disk image |

## Directory Structure

```
installer/
├── build-all.ps1          # Master build script (publish + all installers)
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
