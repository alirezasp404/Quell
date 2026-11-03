#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-1.0.0}"
ARCH="amd64"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="${2:-$ROOT_DIR/publish/linux-x64}"
DIST_DIR="${3:-$ROOT_DIR/dist/linux}"

# Ensure directories exist and resolve to absolute paths
mkdir -p "$DIST_DIR"
DIST_DIR="$(cd "$DIST_DIR" && pwd)"
PUBLISH_DIR="$(cd "$PUBLISH_DIR" && pwd)"

echo "Building Debian package for Fdb Studio v$VERSION ($ARCH)..."
echo "Source publish dir: $PUBLISH_DIR"
echo "Target dist dir:    $DIST_DIR"

PACKAGE_ROOT=$(mktemp -d)
trap 'rm -rf "$PACKAGE_ROOT"' EXIT

# Create packaging directory tree
mkdir -p "$PACKAGE_ROOT/DEBIAN"
mkdir -p "$PACKAGE_ROOT/usr/lib/fdb-studio"
mkdir -p "$PACKAGE_ROOT/usr/bin"
mkdir -p "$PACKAGE_ROOT/usr/share/applications"
mkdir -p "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps"

# Copy published application binaries & assets
cp -r "$PUBLISH_DIR"/* "$PACKAGE_ROOT/usr/lib/fdb-studio/"
if [ -f "$PACKAGE_ROOT/usr/lib/fdb-studio/Fdb" ]; then
    chmod +x "$PACKAGE_ROOT/usr/lib/fdb-studio/Fdb"
fi

# Create symlink in /usr/bin
ln -sf "/usr/lib/fdb-studio/Fdb" "$PACKAGE_ROOT/usr/bin/fdb-studio"

# Copy desktop launcher
cp "$SCRIPT_DIR/fdb-studio.desktop" "$PACKAGE_ROOT/usr/share/applications/fdb-studio.desktop"

# Copy icon if available
if [ -f "$ROOT_DIR/Assets/fdb_icon.png" ]; then
    cp "$ROOT_DIR/Assets/fdb_icon.png" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/fdb-studio.png"
elif [ -f "$ROOT_DIR/Assets/fdb_icon.ico" ]; then
    cp "$ROOT_DIR/Assets/fdb_icon.ico" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/fdb-studio.ico"
elif [ -f "$ROOT_DIR/Assets/logo.png" ]; then
    cp "$ROOT_DIR/Assets/logo.png" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/fdb-studio.png"
elif [ -f "$ROOT_DIR/Assets/logo.ico" ]; then
    cp "$ROOT_DIR/Assets/logo.ico" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/fdb-studio.ico"
elif [ -f "$ROOT_DIR/Assets/avalonia-logo.ico" ]; then
    cp "$ROOT_DIR/Assets/avalonia-logo.ico" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/fdb-studio.ico"
fi

# Write DEBIAN/control
cat <<EOF > "$PACKAGE_ROOT/DEBIAN/control"
Package: fdb-studio
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: CipherUnits <info@cipherunits.local>
Description: Fdb Studio - Modern Cross-Platform Database Client and SQL IDE
 A rich, cross-platform database management tool and SQL IDE built with Avalonia UI.
 Supports SQL Server, SQLite, IntelliSense autocomplete, and modular theming.
EOF

# Calculate installed disk size in KB
INSTALLED_SIZE=$(du -sk "$PACKAGE_ROOT/usr" | awk '{print $1}')
echo "Installed-Size: $INSTALLED_SIZE" >> "$PACKAGE_ROOT/DEBIAN/control"

# Build debian package
DEB_FILE="$DIST_DIR/fdb-studio_${VERSION}_amd64.deb"
dpkg-deb --build --root-owner-group "$PACKAGE_ROOT" "$DEB_FILE"

echo "Debian package created: $DEB_FILE"
