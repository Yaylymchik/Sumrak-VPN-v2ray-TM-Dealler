#!/bin/bash
# Primary macOS launcher binary inside Sumrak VPN.app
set -e
APP_MACOS="$(cd "$(dirname "$0")" && pwd)"
export SUMRAX_ROOT="$APP_MACOS"
cd "$APP_MACOS"

chmod +x "$APP_MACOS/sumrax" 2>/dev/null || true
chmod +x "$APP_MACOS/core/xray/xray" 2>/dev/null || true
chmod +x "$APP_MACOS/scripts/proxy_set_osx.sh" 2>/dev/null || true
xattr -cr "$APP_MACOS/core/xray/xray" 2>/dev/null || true

PY=""
for c in python3 python; do
  if command -v "$c" >/dev/null 2>&1 && "$c" -c "import tkinter" 2>/dev/null; then
    PY="$c"
    break
  fi
done

if [[ -n "$PY" && -f "$APP_MACOS/sumrax_gui.py" ]]; then
  exec "$PY" "$APP_MACOS/sumrax_gui.py"
fi

exec "$APP_MACOS/sumrax" start
