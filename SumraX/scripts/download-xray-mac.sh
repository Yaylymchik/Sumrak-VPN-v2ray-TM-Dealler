#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET_DIR="${1:-$ROOT/runtime/xray}"
FORCE="${2:-}"

mkdir -p "$TARGET_DIR"

if [[ "$FORCE" != "-Force" ]] && [[ -f "$TARGET_DIR/xray" || -f "$TARGET_DIR/xray.exe" ]]; then
  echo "Xray runtime already present in $TARGET_DIR"
  # ensure geo databases
  for geo in geoip.dat geosite.dat; do
    if [[ ! -f "$TARGET_DIR/$geo" ]] && [[ -f "$ROOT/runtime/xray/$geo" ]]; then
      cp "$ROOT/runtime/xray/$geo" "$TARGET_DIR/"
    fi
  done
  exit 0
fi

ARCH="$(uname -m)"
if [[ "$ARCH" == "arm64" ]]; then
  PATTERN="macos-arm64"
else
  PATTERN="macos-64"
fi

echo "Fetching latest Xray-core ($PATTERN)..."
JSON="$(curl -fsSL -H 'User-Agent: SumraX-VPN-Setup' https://api.github.com/repos/XTLS/Xray-core/releases/latest)"
URL="$(python3 - <<'PY' "$JSON" "$PATTERN"
import json, sys
data = json.loads(sys.argv[1])
pattern = sys.argv[2].lower()
for asset in data.get("assets", []):
    name = asset.get("name", "").lower()
    if pattern in name and name.endswith(".zip"):
        print(asset["browser_download_url"])
        break
PY
)"

if [[ -z "$URL" ]]; then
  echo "Xray $PATTERN zip not found in latest release" >&2
  exit 1
fi

TMP_ZIP="$(mktemp -t xray.XXXXXX.zip)"
TMP_DIR="$(mktemp -d -t xray-extract.XXXXXX)"
trap 'rm -rf "$TMP_ZIP" "$TMP_DIR"' EXIT

curl -fsSL -H 'User-Agent: SumraX-VPN-Setup' "$URL" -o "$TMP_ZIP"
unzip -q "$TMP_ZIP" -d "$TMP_DIR"
find "$TMP_DIR" -type f -exec cp -f {} "$TARGET_DIR/" \;
chmod +x "$TARGET_DIR/xray" 2>/dev/null || true

for geo in geoip.dat geosite.dat; do
  if [[ ! -f "$TARGET_DIR/$geo" ]] && [[ -f "$ROOT/runtime/xray/$geo" ]]; then
    cp "$ROOT/runtime/xray/$geo" "$TARGET_DIR/"
  fi
done

if [[ ! -f "$TARGET_DIR/xray" ]]; then
  echo "xray binary not found after extraction" >&2
  exit 1
fi

echo "Xray runtime installed to $TARGET_DIR"
ls -lh "$TARGET_DIR"
