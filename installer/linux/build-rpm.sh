#!/usr/bin/env bash
# Build an RPM package for GitHub Markdown Viewer
# Usage: ./build-rpm.sh
# Prerequisites:
#   - Run publish.ps1 -Runtime linux-x64 first
#   - rpm-build package installed (sudo dnf install rpm-build)

set -euo pipefail

APP_NAME="github-markdown-viewer"
APP_DISPLAY_NAME="GitHub Markdown Viewer"
APP_VERSION="1.0.0"
APP_RELEASE="1"
APP_MAINTAINER="Hannah Vernon"
APP_DESCRIPTION="A cross-platform Markdown viewer and editor with GitHub Flavored Markdown support"
APP_URL="https://github.com/HannahVernon/GithubMarkdownViewer"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="$REPO_ROOT/installer/publish/linux-x64"
OUTPUT_DIR="$REPO_ROOT/installer/output"

# Verify publish output exists
if [ ! -d "$PUBLISH_DIR" ]; then
    echo "ERROR: Publish directory not found: $PUBLISH_DIR"
    echo "Run 'publish.ps1 -Runtime linux-x64' first."
    exit 1
fi

# Set up rpmbuild directory structure
RPM_BUILD_ROOT="$REPO_ROOT/installer/rpmbuild"
rm -rf "$RPM_BUILD_ROOT"
mkdir -p "$RPM_BUILD_ROOT"/{BUILD,RPMS,SOURCES,SPECS,SRPMS,BUILDROOT}
mkdir -p "$OUTPUT_DIR"

# Create the spec file
cat > "$RPM_BUILD_ROOT/SPECS/$APP_NAME.spec" << EOF
Name:           $APP_NAME
Version:        $APP_VERSION
Release:        $APP_RELEASE%{?dist}
Summary:        $APP_DESCRIPTION
License:        MIT
URL:            $APP_URL
BuildArch:      x86_64
AutoReqProv:    no

%description
A cross-platform .NET 9 desktop application for viewing and editing
Markdown files with live preview and full GitHub Flavored Markdown
(GFM) support. Built with Avalonia UI and Markdig.

%install
mkdir -p %{buildroot}/usr/lib/$APP_NAME
cp -r $PUBLISH_DIR/* %{buildroot}/usr/lib/$APP_NAME/
chmod +x %{buildroot}/usr/lib/$APP_NAME/GithubMarkdownViewer

mkdir -p %{buildroot}/usr/bin
ln -sf /usr/lib/$APP_NAME/GithubMarkdownViewer %{buildroot}/usr/bin/$APP_NAME

mkdir -p %{buildroot}/usr/share/applications
cat > %{buildroot}/usr/share/applications/$APP_NAME.desktop << DESKTOP
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
DESKTOP

mkdir -p %{buildroot}/usr/share/doc/$APP_NAME
cp $REPO_ROOT/LICENSE %{buildroot}/usr/share/doc/$APP_NAME/
cp $REPO_ROOT/README.md %{buildroot}/usr/share/doc/$APP_NAME/

%files
/usr/lib/$APP_NAME/
/usr/bin/$APP_NAME
/usr/share/applications/$APP_NAME.desktop
/usr/share/doc/$APP_NAME/

%post
update-desktop-database -q /usr/share/applications 2>/dev/null || true

%postun
update-desktop-database -q /usr/share/applications 2>/dev/null || true
EOF

# Build the RPM
rpmbuild --define "_topdir $RPM_BUILD_ROOT" -bb "$RPM_BUILD_ROOT/SPECS/$APP_NAME.spec"

# Copy RPM to output directory
find "$RPM_BUILD_ROOT/RPMS" -name "*.rpm" -exec cp {} "$OUTPUT_DIR/" \;

# Clean up
rm -rf "$RPM_BUILD_ROOT"

echo ""
echo "RPM package created in: $OUTPUT_DIR/"
echo "Install with: sudo rpm -i $OUTPUT_DIR/${APP_NAME}-${APP_VERSION}-${APP_RELEASE}*.rpm"
