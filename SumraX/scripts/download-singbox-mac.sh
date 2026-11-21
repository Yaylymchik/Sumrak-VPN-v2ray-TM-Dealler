#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ARCH="$(uname -m)"
TARGET_DIR="${1:-$ROOT/runtime/mac-$([ "$ARCH" = "arm64" ] && echo arm64 || echo x64)/sing-box}"
FORCE="${2:-}"

mkdir -p "$TARGET_DIR"

if [[ "$FORCE" != "-Force" ]] && [[ -f "$TARGET_DIR/sing-box" ]]; then
  echo "sing-box already present in $TARGET_DIR"
  exit 0
fi

PATTERN=$([ "$ARCH" = "arm64" ] && echo "darwin-arm64" || echo "darwin-amd64")
echo "Fetching sing-box ($PATTERN)..."
JSON="$(curl -fsSL -H 'User-Agent: SumraX-VPN-Setup' https://api.github.com/repos/SagerNet/sing-box/releases/latest)"
URL="$(python3 - <<'PY' "$JSON" "$PATTERN"
import json, sys
data = json.loads(sys.argv[1])
pattern = sys.argv[2].lower()
for asset in data.get("assets", []):
    name = asset.get("name", "").lower()
    if pattern in name and (name.endswith(".zip") or name.endswith(".tar.gz")):
        print(asset["browser_download_url"])
        break
PY
)"

if [[ -z "$URL" ]]; then
  echo "sing-box $PATTERN not found" >&2
  exit 1
fi

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
FILE="$TMP/archive"
curl -fsSL -H 'User-Agent: SumraX-VPN-Setup' "$URL" -o "$FILE"
if [[ "$URL" == *.tar.gz ]]; then
  tar -xzf "$FILE" -C "$TMP"
else
  unzip -q "$FILE" -d "$TMP"
fi
find "$TMP" -type f -exec cp -f {} "$TARGET_DIR/" \;
chmod +x "$TARGET_DIR/sing-box" 2>/dev/null || true

if [[ ! -f "$TARGET_DIR/sing-box" ]]; then
  echo "sing-box binary not found after extraction" >&2
  exit 1
fi

echo "sing-box installed to $TARGET_DIR"
