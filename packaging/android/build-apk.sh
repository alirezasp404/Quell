#!/usr/bin/env bash
set -e

VERSION="${1:-1.0.0}"
PROJECT_PATH="${2:-Fdb.Android/Fdb.Android.csproj}"
DIST_DIR="${3:-dist/android}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/../.." && pwd)"

mkdir -p "$DIST_DIR"
DIST_DIR="$(cd "$DIST_DIR" && pwd)"

echo "Building Android APK Package (v$VERSION)..."
echo "Project path: $PROJECT_PATH"
echo "Target dist dir: $DIST_DIR"

# Build and package APK in Release configuration
dotnet publish "$PROJECT_PATH" \
  -c Release \
  -p:AndroidPackageFormat=apk \
  -p:ApplicationVersion=1 \
  -p:ApplicationDisplayVersion="$VERSION"

# Locate generated APK and copy to target dist directory
APK_FILE=$(find "Fdb.Android/bin/Release" -name "*Signed.apk" -o -name "*Signed*.apk" -o -name "*.apk" 2>/dev/null | head -n 1)

if [ -n "$APK_FILE" ] && [ -f "$APK_FILE" ]; then
  OUTPUT_NAME="Fdb-Studio-$VERSION-android.apk"
  cp "$APK_FILE" "$DIST_DIR/$OUTPUT_NAME"
  echo "Android APK created successfully at: $DIST_DIR/$OUTPUT_NAME"
else
  echo "Listing generated Android outputs:"
  find "Fdb.Android/bin" -type f || true
fi
