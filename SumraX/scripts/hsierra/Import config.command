#!/bin/bash
cd "$(dirname "$0")"
chmod +x sumrax 2>/dev/null
echo "Перетащи config.json в это окно Terminal и нажми Enter:"
read -e CFG
CFG="${CFG//\'/}"
CFG="${CFG//\"/}"
if [[ -f "$CFG" ]]; then
  ./sumrax import "$CFG"
  osascript -e 'display notification "config импортирован" with title "Sumrak VPN"'
  echo "OK. Теперь запусти «Start Sumrak VPN.command»"
else
  echo "Файл не найден: $CFG"
fi
read -p "Enter для закрытия..."
