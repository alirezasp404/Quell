#!/usr/bin/env bash
set -euo pipefail

RID="${1:-osx-x64}"
VERSION="${2:-1.0.0}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"
PUBLISH_DIR="${3:-$ROOT_DIR/publish/$RID}"
DIST_DIR="${4:-$ROOT_DIR/dist/macos}"

# Ensure directories exist and resolve to absolute paths so subshell cd operations don't break relative paths
mkdir -p "$DIST_DIR"
DIST_DIR="$(cd "$DIST_DIR" && pwd)"
PUBLISH_DIR="$(cd "$PUBLISH_DIR" && pwd)"

echo "Building macOS application bundle and installer for $RID (v$VERSION)..."
echo "Source publish dir: $PUBLISH_DIR"
echo "Target dist dir:    $DIST_DIR"

APP_NAME="Quell.app"
STAGING_DIR=$(mktemp -d)
trap 'rm -rf "$STAGING_DIR"' EXIT

APP_PATH="$STAGING_DIR/$APP_NAME"
CONTENTS_PATH="$APP_PATH/Contents"
MACOS_PATH="$CONTENTS_PATH/MacOS"
RESOURCES_PATH="$CONTENTS_PATH/Resources"

mkdir -p "$MACOS_PATH"
mkdir -p "$RESOURCES_PATH"

# Copy published application files
cp -r "$PUBLISH_DIR"/* "$MACOS_PATH/"
if [ -f "$MACOS_PATH/Quell" ]; then
    chmod +x "$MACOS_PATH/Quell"
elif [ -f "$MACOS_PATH/Fdb" ]; then
    chmod +x "$MACOS_PATH/Fdb"
fi

# Copy Info.plist
cp "$SCRIPT_DIR/Info.plist" "$CONTENTS_PATH/Info.plist"

# Copy Assets to Resources if desired
if [ -d "$ROOT_DIR/Assets" ]; then
    cp -r "$ROOT_DIR/Assets" "$RESOURCES_PATH/"
fi

# Create portable zip archive
ZIP_FILE="$DIST_DIR/Quell-${VERSION}-${RID}.zip"
(cd "$STAGING_DIR" && zip -r -y "$ZIP_FILE" "$APP_NAME")
echo "Created zip bundle: $ZIP_FILE"

# Create .dmg if hdiutil is available
if command -v hdiutil &>/dev/null; then
    DMG_DIR="$STAGING_DIR/dmg_stage"
    mkdir -p "$DMG_DIR"
    cp -R "$APP_PATH" "$DMG_DIR/"
    ln -s /Applications "$DMG_DIR/Applications"

    DMG_FILE="$DIST_DIR/Quell-${VERSION}-${RID}.dmg"
    rm -f "$DMG_FILE"
    hdiutil create -volname "Quell" -srcfolder "$DMG_DIR" -ov -format UDZO "$DMG_FILE"
    echo "Created DMG installer: $DMG_FILE"
fi

echo "macOS packaging completed for $RID."
