#!/bin/bash
cd "$(dirname "$0")"
chmod +x sumrax scripts/proxy_set_osx.sh bin/xray/xray 2>/dev/null
xattr -cr bin/xray/xray 2>/dev/null || true
if [[ ! -f config/config.json ]]; then
  osascript -e 'display alert "Нет config.json" message "Сначала: ./sumrax import /path/to/config.json\nИли положи config.json в папку config/"'
  exit 1
fi
./sumrax start
osascript -e 'display notification "VPN запущен" with title "Sumrak VPN"'
echo ""
echo "Sumrak VPN работает. Окно можно закрыть."
echo "Стоп: двойной клик «Stop Sumrak VPN.command»"
read -p "Enter для закрытия..."
