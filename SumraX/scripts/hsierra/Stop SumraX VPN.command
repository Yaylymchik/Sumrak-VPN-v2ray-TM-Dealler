#!/bin/bash
cd "$(dirname "$0")"
chmod +x sumrax 2>/dev/null
./sumrax stop
osascript -e 'display notification "VPN остановлен" with title "Sumrak VPN"'
echo "Остановлено."
read -p "Enter для закрытия..."
