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

echo "Building Debian package for Quell v$VERSION ($ARCH)..."
echo "Source publish dir: $PUBLISH_DIR"
echo "Target dist dir:    $DIST_DIR"

PACKAGE_ROOT=$(mktemp -d)
trap 'rm -rf "$PACKAGE_ROOT"' EXIT

# Create packaging directory tree
mkdir -p "$PACKAGE_ROOT/DEBIAN"
mkdir -p "$PACKAGE_ROOT/usr/lib/quell"
mkdir -p "$PACKAGE_ROOT/usr/bin"
mkdir -p "$PACKAGE_ROOT/usr/share/applications"
mkdir -p "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps"

# Copy published application binaries & assets
cp -r "$PUBLISH_DIR"/* "$PACKAGE_ROOT/usr/lib/quell/"
if [ -f "$PACKAGE_ROOT/usr/lib/quell/Quell" ]; then
    chmod +x "$PACKAGE_ROOT/usr/lib/quell/Quell"
elif [ -f "$PACKAGE_ROOT/usr/lib/quell/Fdb" ]; then
    chmod +x "$PACKAGE_ROOT/usr/lib/quell/Fdb"
fi

# Create symlink in /usr/bin
if [ -f "$PACKAGE_ROOT/usr/lib/quell/Quell" ]; then
    ln -sf "/usr/lib/quell/Quell" "$PACKAGE_ROOT/usr/bin/quell"
else
    ln -sf "/usr/lib/quell/Fdb" "$PACKAGE_ROOT/usr/bin/quell"
fi

# Copy desktop launcher
cp "$SCRIPT_DIR/quell.desktop" "$PACKAGE_ROOT/usr/share/applications/quell.desktop"

# Copy icon if available
if [ -f "$ROOT_DIR/Assets/quell_icon.png" ]; then
    cp "$ROOT_DIR/Assets/quell_icon.png" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/quell.png"
elif [ -f "$ROOT_DIR/Assets/fdb_icon.png" ]; then
    cp "$ROOT_DIR/Assets/fdb_icon.png" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/quell.png"
elif [ -f "$ROOT_DIR/Assets/quell_icon.ico" ]; then
    cp "$ROOT_DIR/Assets/quell_icon.ico" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/quell.ico"
elif [ -f "$ROOT_DIR/Assets/logo.png" ]; then
    cp "$ROOT_DIR/Assets/logo.png" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/quell.png"
elif [ -f "$ROOT_DIR/Assets/logo.ico" ]; then
    cp "$ROOT_DIR/Assets/logo.ico" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/quell.ico"
elif [ -f "$ROOT_DIR/Assets/avalonia-logo.ico" ]; then
    cp "$ROOT_DIR/Assets/avalonia-logo.ico" "$PACKAGE_ROOT/usr/share/icons/hicolor/256x256/apps/quell.ico"
fi

# Write DEBIAN/control
cat <<EOF > "$PACKAGE_ROOT/DEBIAN/control"
Package: quell
Version: $VERSION
Section: utils
Priority: optional
Architecture: $ARCH
Maintainer: CipherUnits <info@cipherunits.local>
Description: Quell - Modern Cross-Platform Database Client and SQL IDE
 A rich, cross-platform database management tool and SQL IDE built with Avalonia UI.
 Supports SQL Server, SQLite, IntelliSense autocomplete, and modular theming.
EOF

# Calculate installed disk size in KB
INSTALLED_SIZE=$(du -sk "$PACKAGE_ROOT/usr" | awk '{print $1}')
echo "Installed-Size: $INSTALLED_SIZE" >> "$PACKAGE_ROOT/DEBIAN/control"

# Build debian package
DEB_FILE="$DIST_DIR/quell_${VERSION}_amd64.deb"
dpkg-deb --build --root-owner-group "$PACKAGE_ROOT" "$DEB_FILE"

echo "Debian package created: $DEB_FILE"
