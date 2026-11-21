#!/usr/bin/env bash
# SumraX DMG like Sumrak.macOS.universal.dmg (Sumrak.app + Applications link)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/dist-hsierra"
DMG="$OUT/SumraX-VPN-macOS-x64.dmg"
STAGE="$OUT/Sumrak VPN 1.0"
HS="$ROOT/scripts/hsierra"
APP="$OUT/Sumrak VPN.app"
XRAY_SRC="$ROOT/runtime/mac-x64/xray"
MACOS="$APP/Contents/MacOS"

if [[ ! -f "$XRAY_SRC/xray" ]]; then
  echo "Missing $XRAY_SRC/xray" >&2
  exit 1
fi

rm -rf "$APP"
mkdir -p "$MACOS/core/xray" "$MACOS/config" "$MACOS/data" "$MACOS/scripts"
cp -R "$XRAY_SRC/"* "$MACOS/core/xray/"
chmod +x "$MACOS/core/xray/xray"
cp "$HS/sumrax" "$HS/sumrax_gui.py" "$MACOS/"
chmod +x "$MACOS/sumrax"
cp "$ROOT/src/SumraX.Infrastructure/Resources/proxy_set_osx.sh" "$MACOS/scripts/"
chmod +x "$MACOS/scripts/proxy_set_osx.sh"
cp "$HS/Info.plist" "$APP/Contents/"
cp "$HS/macos-app-launcher.sh" "$MACOS/Sumrak VPN"
chmod +x "$MACOS/Sumrak VPN"

[[ -f "$ROOT/../config.json" ]] && cp "$ROOT/../config.json" "$MACOS/config/config.json" || true
[[ -f "$OUT/../SumraX-HighSierra/config/config.json" ]] && cp "$OUT/../SumraX-HighSierra/config/config.json" "$MACOS/config/config.json" || true

rm -rf "$STAGE"
mkdir -p "$STAGE"
cp -R "$APP" "$STAGE/"
ln -sf /Applications "$STAGE/Applications"

if command -v hdiutil >/dev/null 2>&1; then
  rm -f "$DMG"
  hdiutil create -volname "Sumrak VPN" -srcfolder "$STAGE" -ov -format UDZO "$DMG"
  echo "DMG: $DMG (like Sumrak.macOS.universal.dmg)"
  echo "open \"$DMG\""
else
  echo "Stage: $STAGE (copy to Mac and run hdiutil)"
fi

du -sh "$APP"
