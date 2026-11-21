# Windows 7 / 8.1 и слабые ПК

## Умный установщик (один EXE)

**`SumraX-VPN-Setup-2.2.21-Smart.exe`** — определяет Windows и разрядность, предлагает рекомендуемую сборку и запускает нужный дочерний установщик:

| Вариант | Когда |
|---------|--------|
| Win10/11 x64 | Современный ПК (по умолчанию) |
| Win10/11 x86 | 32-bit Windows |
| Win7/8.1 x64 / x86 | `net6.0-windows7.0` + .NET 6 Runtime |

Сборка: `.\scripts\build-smart-installer.ps1` (нужны все 4 payload в `dist\`).

## Основная ветка (Windows 10 / 11)

- **ОС:** Windows 10 (1607+) и Windows 11  
- **Разрядность:** **x64** и **x86** — self-contained, скрипт `build-all-platforms.ps1`  
- **Слабый ПК:** профиль **Lite** (`-Lite`) — framework-dependent на .NET 8 Desktop Runtime  

```powershell
cd D:\Sumrak\v2rayN-master\v2rayN-master\SumraX
.\scripts\build-all-platforms.ps1 -Lite -BuildInstaller
```

## Windows 7 SP1 / Windows 8.1 (legacy)

Microsoft **не поддерживает .NET 8** на Windows 7. Для этих ОС собирается отдельный target **`net6.0-windows7.0`**:

| Параметр | Значение |
|----------|----------|
| TFM | `net6.0-windows7.0` |
| Publish | Framework-dependent (нужен [.NET 6 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/6.0)) |
| Папки | `publish-win7\` (x64), `publish-win7-x86\` |
| Установщики | `SumraX-VPN-Setup-2.2.21-Win7.exe`, `…-Win7-x86.exe` |

```powershell
cd D:\Sumrak\v2rayN-master\v2rayN-master\SumraX
.\scripts\build-legacy-win7.ps1 -BuildInstaller -SkipXrayDownload
```

Артефакты также копируются в `D:\Sumrak\` скриптом `package-ideal-release.ps1`.

### Рекомендации на Win7/8.1

- Установщики **Win7** / **Lite** сами ставят **.NET 6** или **.NET 8 Desktop Runtime** из офлайн-бандла (если ещё нет в системе). Ручная установка не обязательна.  
- В приложении предпочтительнее **режим системного прокси**, а не TUN (драйверы и wintun на старых ОС ограничены).  
- Ядра **xray / sing-box** должны быть протестированы на реальной машине — бинарники те же, что в `runtime\`.

### Предупреждение

.NET 6 достиг конца поддержки Microsoft; сборка предназначена только для пользователей, которые **не могут** обновиться до Windows 10.

## Слабый ПК — настройки в приложении

- Режим **System proxy** вместо TUN.  
- Отключить тяжёлые анимации (при необходимости).  
- **Lite** (.NET 8) или **Win7** (.NET 6) — меньший объём и RAM при старте, чем self-contained .NET 8.