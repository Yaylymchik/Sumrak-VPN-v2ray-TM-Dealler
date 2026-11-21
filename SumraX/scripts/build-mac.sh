#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Release}"
RID="${2:-}"

if [[ -z "$RID" ]]; then
  if [[ "$(uname -m)" == "arm64" ]]; then
    RID="osx-arm64"
  else
    RID="osx-x64"
  fi
fi

echo "=== Sumrak VPN macOS Build ($RID) ==="

bash "$ROOT/scripts/download-xray-mac.sh"

DESKTOP="$ROOT/src/SumraX.App.Desktop"
PUBLISH="$ROOT/publish-mac"

dotnet publish "$DESKTOP/SumraX.App.Desktop.csproj" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$PUBLISH"

if [[ -d "$ROOT/runtime/xray" ]]; then
  mkdir -p "$PUBLISH/bin/xray"
  cp -R "$ROOT/runtime/xray/"* "$PUBLISH/bin/xray/"
  chmod +x "$PUBLISH/bin/xray/xray" 2>/dev/null || true
fi

APP_NAME="Sumrak VPN.app"
APP_DIR="$PUBLISH/$APP_NAME"
CONTENTS="$APP_DIR/Contents"
MACOS="$CONTENTS/MacOS"
RESOURCES="$CONTENTS/Resources"

rm -rf "$APP_DIR"
mkdir -p "$MACOS" "$RESOURCES"

cp "$PUBLISH/Sumrak VPN" "$MACOS/Sumrak VPN"
chmod +x "$MACOS/Sumrak VPN"
cp -R "$PUBLISH/bin" "$MACOS/bin" 2>/dev/null || true

cat > "$CONTENTS/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>
  <string>Sumrak VPN</string>
  <key>CFBundleDisplayName</key>
  <string>Sumrak VPN</string>
  <key>CFBundleIdentifier</key>
  <string>com.sumrax.vpn</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleExecutable</key>
  <string>Sumrak VPN</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>LSMinimumSystemVersion</key>
  <string>11.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

if [[ -f "$ROOT/src/SumraX.App.Desktop/Assets/sumrax-icon.png" ]]; then
  cp "$ROOT/src/SumraX.App.Desktop/Assets/sumrax-icon.png" "$RESOURCES/icon.png"
fi

echo ""
echo "=== BUILD COMPLETE ==="
echo "App bundle: $APP_DIR"
echo "Run: open \"$APP_DIR\""
