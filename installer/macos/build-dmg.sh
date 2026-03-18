#!/usr/bin/env bash
# Build a macOS .app bundle and .dmg disk image for GitHub Markdown Viewer
# Usage: ./build-dmg.sh
# Prerequisites: Run publish.ps1 -Runtime osx-x64 first (or dotnet publish)

set -euo pipefail

APP_NAME="GitHub Markdown Viewer"
APP_BUNDLE_ID="com.hannahvernon.githubmarkdownviewer"
APP_VERSION="1.0.0"
APP_EXECUTABLE="GithubMarkdownViewer"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/installer/publish/osx-x64"
OUTPUT_DIR="$REPO_ROOT/installer/output"
STAGING_DIR="$REPO_ROOT/installer/staging-macos"

# Verify publish output exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "ERROR: Publish directory not found: $PUBLISH_DIR"
    echo "Run 'publish.ps1 -Runtime osx-x64' first."
    exit 1
fi

# Clean and create staging
rm -rf "$STAGING_DIR"
mkdir -p "$OUTPUT_DIR"

APP_BUNDLE="$STAGING_DIR/$APP_NAME.app"
CONTENTS="$APP_BUNDLE/Contents"
MACOS_DIR="$CONTENTS/MacOS"
RESOURCES_DIR="$CONTENTS/Resources"

mkdir -p "$MACOS_DIR"
mkdir -p "$RESOURCES_DIR"

# Copy published files into MacOS directory
cp -r "$PUBLISH_DIR/"* "$MACOS_DIR/"
chmod +x "$MACOS_DIR/$APP_EXECUTABLE"

# Create Info.plist
cat > "$CONTENTS/Info.plist" << EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleName</key>
    <string>$APP_NAME</string>
    <key>CFBundleDisplayName</key>
    <string>$APP_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>$APP_BUNDLE_ID</string>
    <key>CFBundleVersion</key>
    <string>$APP_VERSION</string>
    <key>CFBundleShortVersionString</key>
    <string>$APP_VERSION</string>
    <key>CFBundleExecutable</key>
    <string>$APP_EXECUTABLE</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleIconFile</key>
    <string>AppIcon</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>LSMinimumSystemVersion</key>
    <string>10.15</string>
    <key>CFBundleDocumentTypes</key>
    <array>
        <dict>
            <key>CFBundleTypeName</key>
            <string>Markdown Document</string>
            <key>CFBundleTypeRole</key>
            <string>Editor</string>
            <key>LSHandlerRank</key>
            <string>Default</string>
            <key>CFBundleTypeExtensions</key>
            <array>
                <string>md</string>
                <string>markdown</string>
                <string>mdown</string>
                <string>mkdn</string>
            </array>
            <key>LSItemContentTypes</key>
            <array>
                <string>net.daringfireball.markdown</string>
            </array>
        </dict>
    </array>
    <key>UTImportedTypeDeclarations</key>
    <array>
        <dict>
            <key>UTTypeConformsTo</key>
            <array>
                <string>public.text</string>
            </array>
            <key>UTTypeDescription</key>
            <string>Markdown Document</string>
            <key>UTTypeIdentifier</key>
            <string>net.daringfireball.markdown</string>
            <key>UTTypeTagSpecification</key>
            <dict>
                <key>public.filename-extension</key>
                <array>
                    <string>md</string>
                    <string>markdown</string>
                    <string>mdown</string>
                    <string>mkdn</string>
                </array>
            </dict>
        </dict>
    </array>
</dict>
</plist>
EOF

# Copy LICENSE into Resources
cp "$REPO_ROOT/LICENSE" "$RESOURCES_DIR/"

echo "App bundle created: $APP_BUNDLE"

# Create DMG
DMG_NAME="GithubMarkdownViewer-${APP_VERSION}-osx-x64"
DMG_PATH="$OUTPUT_DIR/$DMG_NAME.dmg"
DMG_STAGING="$STAGING_DIR/dmg-staging"

mkdir -p "$DMG_STAGING"
cp -r "$APP_BUNDLE" "$DMG_STAGING/"

# Create a symbolic link to /Applications for drag-and-drop install
ln -sf /Applications "$DMG_STAGING/Applications"

# Create the DMG
if command -v hdiutil > /dev/null 2>&1; then
    hdiutil create -volname "$APP_NAME" \
        -srcfolder "$DMG_STAGING" \
        -ov -format UDZO \
        "$DMG_PATH"
    echo "DMG created: $DMG_PATH"
else
    echo "WARNING: hdiutil not available (not on macOS). Skipping DMG creation."
    echo "The .app bundle is available at: $APP_BUNDLE"

    # Create a tar.gz as fallback
    TAR_PATH="$OUTPUT_DIR/$DMG_NAME.tar.gz"
    tar -czf "$TAR_PATH" -C "$STAGING_DIR" "$APP_NAME.app"
    echo "Created tar.gz fallback: $TAR_PATH"
fi

# Clean up staging
rm -rf "$STAGING_DIR"

echo ""
echo "macOS build complete."
