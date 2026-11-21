#!/usr/bin/env bash
# Package Sumrak VPN.app → offline .dmg (built app from build-mac-offline.sh)
set -euo pipefail

Arch="${1:-arm64}"
Version="${2:-1.0.0}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP_SRC="$ROOT/publish-mac/Sumrak VPN.app"
DIST="$ROOT/dist-mac"
DMG_OUT="$DIST/SumraX-VPN-macOS-$Arch-offline.dmg"
STAGE="$DIST/dmg-stage-$Arch"

if [[ ! -d "$APP_SRC" ]]; then
  echo "Missing $APP_SRC — run ./scripts/build-mac-offline.sh first" >&2
  exit 1
fi

mkdir -p "$DIST"
rm -rf "$STAGE" "$DMG_OUT"
mkdir -p "$STAGE"
cp -R "$APP_SRC" "$STAGE/"
ln -sf /Applications "$STAGE/Applications"

# Native macOS — no extra tools required
if command -v hdiutil >/dev/null 2>&1; then
  hdiutil create \
    -volname "Sumrak VPN" \
    -srcfolder "$STAGE" \
    -ov -format UDZO \
    "$DMG_OUT"
  rm -rf "$STAGE"
  DMG_MB=$(du -sm "$DMG_OUT" | cut -f1)
  echo "DMG: $DMG_OUT (${DMG_MB} MB)"
  echo "Install: open \"$DMG_OUT\" → drag Sumrak VPN to Applications"
  exit 0
fi

# Fancy installer UI (optional)
if command -v create-dmg >/dev/null 2>&1; then
  rm -rf "$STAGE/Applications"
  create-dmg \
    --volname "Sumrak VPN" \
    --window-size 700 420 \
    --icon-size 100 \
    --icon "Sumrak VPN.app" 160 185 \
    --hide-extension "Sumrak VPN.app" \
    --app-drop-link 500 185 \
    "$DMG_OUT" \
    "$STAGE/Sumrak VPN.app"
  rm -rf "$STAGE"
  echo "DMG: $DMG_OUT"
  exit 0
fi

echo "hdiutil/create-dmg not found — run on macOS" >&2
exit 1
