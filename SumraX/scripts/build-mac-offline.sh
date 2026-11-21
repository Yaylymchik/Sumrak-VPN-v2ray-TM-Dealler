#!/usr/bin/env bash
# Full offline macOS build — no internet required if runtime/mac-* is pre-downloaded.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
CONFIG="${1:-Release}"
RID="${2:-}"

if [[ -z "$RID" ]]; then
  if [[ "$(uname -m)" == "arm64" ]]; then
    RID="osx-arm64"
    MAC_ARCH="arm64"
  else
    RID="osx-x64"
    MAC_ARCH="x64"
  fi
else
  case "$RID" in
    osx-arm64) MAC_ARCH="arm64" ;;
    osx-x64)   MAC_ARCH="x64" ;;
    *) echo "Unsupported RID: $RID (use osx-arm64 or osx-x64)" >&2; exit 1 ;;
  esac
fi

RUNTIME="$ROOT/runtime/mac-$MAC_ARCH"
XRAY_SRC="$RUNTIME/xray"
SING_SRC="$RUNTIME/sing-box"
DESKTOP="$ROOT/src/SumraX.App.Desktop"
PUBLISH="$ROOT/publish-mac"
DIST="$ROOT/dist-mac"

echo "=== Sumrak VPN macOS OFFLINE BUILD ($RID) ==="

if [[ ! -f "$XRAY_SRC/xray" ]]; then
  echo "Missing bundled xray at: $XRAY_SRC/xray" >&2
  echo "Run once while online:" >&2
  echo "  Windows: .\\scripts\\download-mac-runtimes.ps1" >&2
  echo "  Mac:     ./scripts/download-xray-mac.sh && ./scripts/download-singbox-mac.sh" >&2
  exit 1
fi

for f in geoip.dat geosite.dat; do
  if [[ ! -f "$XRAY_SRC/$f" ]]; then
    echo "WARNING: missing $f in $XRAY_SRC" >&2
  fi
done

dotnet publish "$DESKTOP/SumraX.App.Desktop.csproj" \
  -c "$CONFIG" \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "$PUBLISH"

mkdir -p "$PUBLISH/bin/xray" "$PUBLISH/bin/sing-box"
cp -R "$XRAY_SRC/"* "$PUBLISH/bin/xray/"
chmod +x "$PUBLISH/bin/xray/xray"

if [[ -f "$SING_SRC/sing-box" ]]; then
  cp -R "$SING_SRC/"* "$PUBLISH/bin/sing-box/"
  chmod +x "$PUBLISH/bin/sing-box/sing-box"
  echo "sing-box bundled"
else
  echo "sing-box not bundled (optional protocols may be unavailable offline)"
fi

# Offline marker — app will never try to download cores at runtime
echo "offline" > "$PUBLISH/.sumrax-offline-bundle"
echo "1.0.0" > "$PUBLISH/version.json"

# Integrity manifest
python3 - <<'PY' "$PUBLISH" "$ROOT/publish-mac/bin/integrity.json"
import hashlib, json, os, sys
root = sys.argv[1]
out = sys.argv[2]
files = {}
for rel in ("bin/xray/xray", "bin/sing-box/sing-box"):
    path = os.path.join(root, rel)
    if os.path.isfile(path):
        h = hashlib.sha256(open(path, "rb").read()).hexdigest()
        files[rel] = h
os.makedirs(os.path.dirname(out), exist_ok=True)
json.dump({"files": files}, open(out, "w"), indent=2)
PY

APP_NAME="Sumrak VPN.app"
APP_DIR="$PUBLISH/$APP_NAME"
CONTENTS="$APP_DIR/Contents"
MACOS="$CONTENTS/MacOS"
RESOURCES="$CONTENTS/Resources"

rm -rf "$APP_DIR"
mkdir -p "$MACOS" "$RESOURCES"

cp "$PUBLISH/Sumrak VPN" "$MACOS/Sumrak VPN"
chmod +x "$MACOS/Sumrak VPN"
cp -R "$PUBLISH/bin" "$MACOS/bin"
cp "$PUBLISH/.sumrax-offline-bundle" "$MACOS/.sumrax-offline-bundle"
cp "$PUBLISH/bin/integrity.json" "$MACOS/bin/integrity.json" 2>/dev/null || true
cp "$PUBLISH/version.json" "$MACOS/version.json"

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
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.utilities</string>
</dict>
</plist>
PLIST

if [[ -f "$ROOT/src/SumraX.App.Desktop/Assets/sumrax-icon.png" ]]; then
  cp "$ROOT/src/SumraX.App.Desktop/Assets/sumrax-icon.png" "$RESOURCES/icon.png"
fi

mkdir -p "$DIST"
ARCHIVE="$DIST/SumraX-VPN-macOS-$MAC_ARCH-offline.zip"
rm -f "$ARCHIVE"
( cd "$PUBLISH" && zip -r -q "$ARCHIVE" "$APP_NAME" )

echo ""
echo "=== Building DMG ==="
bash "$ROOT/scripts/package-osx.sh" "$MAC_ARCH"

TOTAL_MB=$(du -sm "$APP_DIR" | cut -f1)
ZIP_MB=$(du -sm "$ARCHIVE" | cut -f1)
DMG_FILE="$DIST/SumraX-VPN-macOS-$MAC_ARCH-offline.dmg"
DMG_MB=""
if [[ -f "$DMG_FILE" ]]; then
  DMG_MB=$(du -sm "$DMG_FILE" | cut -f1)
fi

echo ""
echo "=== OFFLINE BUILD COMPLETE ==="
echo "App:     $APP_DIR"
echo "DMG:     $DMG_FILE${DMG_MB:+ (${DMG_MB} MB)}"
echo "Zip:     $ARCHIVE (${ZIP_MB} MB)"
echo "Size:    app ${TOTAL_MB} MB"
echo "Install: open \"$DMG_FILE\""
echo ""
echo "No internet needed after install (cores + geo bundled)."
