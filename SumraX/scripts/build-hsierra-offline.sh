#!/usr/bin/env bash
# Package SumraX CLI for macOS 10.13 High Sierra (Intel x64, no .NET)
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/dist-hsierra/SumraX-VPN-CLI"
DIST="$ROOT/dist-hsierra"
XRAY_SRC="$ROOT/runtime/mac-x64/xray"
DMG="$DIST/SumraX-VPN-HighSierra-x64.dmg"

if [[ ! -f "$XRAY_SRC/xray" ]]; then
  echo "Missing $XRAY_SRC/xray — run download-mac-runtimes.ps1 -Arch x64 on Windows" >&2
  exit 1
fi

rm -rf "$OUT"
mkdir -p "$OUT/bin/xray" "$OUT/scripts" "$OUT/config" "$OUT/data"

cp -R "$XRAY_SRC/"* "$OUT/bin/xray/"
chmod +x "$OUT/bin/xray/xray"

cp "$ROOT/scripts/hsierra/sumrax" "$OUT/sumrax"
chmod +x "$OUT/sumrax"

cp "$ROOT/src/SumraX.Infrastructure/Resources/proxy_set_osx.sh" "$OUT/scripts/proxy_set_osx.sh"
chmod +x "$OUT/scripts/proxy_set_osx.sh"

cat > "$OUT/config/config.json.example" <<'JSON'
{
  "log": { "loglevel": "warning" },
  "inbounds": [
    { "tag": "socks", "port": 10808, "listen": "127.0.0.1", "protocol": "socks" },
    { "tag": "http", "port": 10809, "listen": "127.0.0.1", "protocol": "http" }
  ],
  "outbounds": [
    { "tag": "proxy", "protocol": "vless", "settings": { "vnext": [] } },
    { "tag": "direct", "protocol": "freedom" },
    { "tag": "block", "protocol": "blackhole" }
  ],
  "routing": { "domainStrategy": "IPIfNonMatch", "rules": [{ "type": "field", "network": "tcp,udp", "outboundTag": "proxy" }] }
}
JSON

cat > "$OUT/README-HighSierra.txt" <<'TXT'
Sumrak VPN — macOS 10.13 High Sierra (CLI, NO .NET)

The GUI app needs macOS 11+ and .NET 8. This bundle works on High Sierra.

1. Copy this folder anywhere (e.g. Applications/SumraX-VPN-CLI)
2. Get config.json from SumraX on Windows:
   %AppData%\Sumrak VPN\config.json
   Or build from your server export.
3. Terminal:
   cd /path/to/SumraX-VPN-CLI
   ./sumrax import /path/to/config.json
   ./sumrax start
4. Stop: ./sumrax stop

Ports: SOCKS 10808, HTTP 10809 (system proxy uses HTTP port)
TXT

mkdir -p "$DIST"
STAGE="$DIST/dmg-hsierra-stage"
rm -rf "$STAGE" "$DMG"
mkdir -p "$STAGE"
cp -R "$OUT/." "$STAGE/"

if command -v hdiutil >/dev/null 2>&1; then
  hdiutil create -volname "Sumrak VPN CLI" -srcfolder "$STAGE" -ov -format UDZO "$DMG"
  rm -rf "$STAGE"
  echo "DMG: $DMG"
else
  ZIP="$DIST/SumraX-VPN-HighSierra-x64.zip"
  ( cd "$DIST" && zip -r -q "$ZIP" "SumraX-VPN-CLI" )
  echo "Zip: $ZIP (run on Mac to create dmg)"
fi

echo "Folder: $OUT"
du -sh "$OUT"
