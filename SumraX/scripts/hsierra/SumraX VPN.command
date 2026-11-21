#!/bin/bash
cd "$(dirname "$0")"
chmod +x sumrax sumrax_gui.py scripts/proxy_set_osx.sh bin/xray/xray 2>/dev/null
xattr -cr bin/xray/xray 2>/dev/null || true

PY=""
for c in python3 python; do
  if command -v "$c" >/dev/null 2>&1; then
    if "$c" -c "import tkinter" 2>/dev/null; then
      PY="$c"
      break
    fi
  fi
done

if [[ -z "$PY" ]]; then
  osascript -e 'display alert "Нужен Python с tkinter" message "Установи python3 с python.org или используй Start Sumrak VPN.command"'
  exit 1
fi

exec "$PY" sumrax_gui.py
