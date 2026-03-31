#!/usr/bin/env bash
# Build a .deb package for GitHub Markdown Viewer
# Usage: ./build-deb.sh
# Prerequisites: Run publish.ps1 -Runtime linux-x64 first (or use dotnet publish)

set -euo pipefail
umask 077

APP_NAME="github-markdown-viewer"
APP_DISPLAY_NAME="GitHub Markdown Viewer"
APP_VERSION="1.0.0"
APP_MAINTAINER="Hannah Vernon"
APP_DESCRIPTION="A cross-platform Markdown viewer and editor with GitHub Flavored Markdown support"
APP_URL="https://github.com/HannahVernon/GithubMarkdownViewer"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/installer/publish/linux-x64"
OUTPUT_DIR="$REPO_ROOT/installer/output"
STAGING_DIR="$REPO_ROOT/installer/staging-deb"

# Verify publish output exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "ERROR: Publish directory not found: $PUBLISH_DIR"
    echo "Run 'publish.ps1 -Runtime linux-x64' first."
    exit 1
fi

# Clean and create staging directory
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR/DEBIAN"
mkdir -p "$STAGING_DIR/usr/lib/$APP_NAME"
mkdir -p "$STAGING_DIR/usr/bin"
mkdir -p "$STAGING_DIR/usr/share/applications"
mkdir -p "$STAGING_DIR/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$STAGING_DIR/usr/share/doc/$APP_NAME"
mkdir -p "$OUTPUT_DIR"

# Copy published files
cp -r "$PUBLISH_DIR/"* "$STAGING_DIR/usr/lib/$APP_NAME/"
chmod +x "$STAGING_DIR/usr/lib/$APP_NAME/GithubMarkdownViewer"

# Create symlink in /usr/bin
ln -sf "/usr/lib/$APP_NAME/GithubMarkdownViewer" "$STAGING_DIR/usr/bin/$APP_NAME"

# Copy docs
cp "$REPO_ROOT/LICENSE" "$STAGING_DIR/usr/share/doc/$APP_NAME/"
cp "$REPO_ROOT/README.md" "$STAGING_DIR/usr/share/doc/$APP_NAME/"

# Create .desktop file
cat > "$STAGING_DIR/usr/share/applications/$APP_NAME.desktop" << EOF
[Desktop Entry]
Name=$APP_DISPLAY_NAME
Comment=$APP_DESCRIPTION
Exec=/usr/bin/$APP_NAME %f
Icon=$APP_NAME
Terminal=false
Type=Application
Categories=Utility;TextEditor;Development;
MimeType=text/markdown;text/x-markdown;
Keywords=markdown;md;editor;viewer;preview;github;
StartupWMClass=GithubMarkdownViewer
EOF

# Create DEBIAN/control
INSTALLED_SIZE=$(du -sk "$STAGING_DIR/usr" | cut -f1)
cat > "$STAGING_DIR/DEBIAN/control" << EOF
Package: $APP_NAME
Version: $APP_VERSION
Section: editors
Priority: optional
Architecture: amd64
Installed-Size: $INSTALLED_SIZE
Maintainer: $APP_MAINTAINER
Description: $APP_DESCRIPTION
 A cross-platform .NET 9 desktop application for viewing and editing
 Markdown files with live preview and full GitHub Flavored Markdown
 (GFM) support. Built with Avalonia UI and Markdig.
Homepage: $APP_URL
EOF

# Create DEBIAN/postinst to update desktop database
cat > "$STAGING_DIR/DEBIAN/postinst" << 'EOF'
#!/bin/sh
set -e
if command -v update-desktop-database > /dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
if command -v gtk-update-icon-cache > /dev/null 2>&1; then
    gtk-update-icon-cache -q /usr/share/icons/hicolor || true
fi
EOF
chmod 755 "$STAGING_DIR/DEBIAN/postinst"

# Create DEBIAN/postrm
cat > "$STAGING_DIR/DEBIAN/postrm" << 'EOF'
#!/bin/sh
set -e
if command -v update-desktop-database > /dev/null 2>&1; then
    update-desktop-database -q /usr/share/applications || true
fi
EOF
chmod 755 "$STAGING_DIR/DEBIAN/postrm"

# Build the .deb
DEB_FILE="$OUTPUT_DIR/${APP_NAME}_${APP_VERSION}_amd64.deb"
dpkg-deb --build --root-owner-group "$STAGING_DIR" "$DEB_FILE"

# Clean up staging
rm -rf "$STAGING_DIR"

echo ""
echo "Package created: $DEB_FILE"
echo "Install with: sudo dpkg -i $DEB_FILE"
