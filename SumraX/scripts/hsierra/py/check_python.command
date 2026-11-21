#!/bin/bash
# Проверка Python и tkinter на Mac
cd "$(dirname "$0")/.."
echo "=== SumraX Python check ==="
for c in python3 python; do
  if command -v "$c" >/dev/null 2>&1; then
    ver=$("$c" --version 2>&1)
    echo "Found: $c ($ver)"
    if "$c" -c "import tkinter; print('tkinter: OK')" 2>/dev/null; then
      echo ">>> Используй: Sumrak VPN.command"
      read -p "Enter..."
      exit 0
    else
      echo "  tkinter: НЕТ"
    fi
  fi
done
echo ""
echo "Python с tkinter не найден."
echo "Варианты:"
echo "  1) python.org/downloads — Python 3 для Mac"
echo "  2) Start Sumrak VPN.command — без GUI"
read -p "Enter..."
