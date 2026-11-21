# Android (Sumrak VPN 2.2.x) ↔ Desktop (SumraX PC)

| Область | Android `v2rayNG-master` | PC `SumraX` | Действие |
|---------|--------------------------|-------------|----------|
| Бренд / имя | Sumrak VPN, `com.sumrax.app` | Sumrak VPN.exe | OK |
| Версия | 2.2.21 (751) | 2.2.21 installers + portable | OK |
| Иконка | `mipmap-xxxhdpi/ic_launcher.png` | `sync-android-icon.ps1` → `sumrax.ico` / PNG | Синхронизировать перед релизом |
| Порядок в шторке серверов | пинг → обновить → импорт | `ServerPickerOverlay.xaml` | OK (как Android) |
| Сортировка узлов | Smart / ping / имя / нагрузка + автосорт после теста | `ServersViewModel` + настройка | OK |
| crypt1–4 ссылки | RSA в native / resolver | `CryptLinkDecryptor.cs` | OK |
| **crypt5** | JNI `liberror-code.so` | `Crypt5LinkDecryptor` (C#) | Проверить на реальных crypt5 ссылках |
| sumrax:// схемы | Manifest + `UrlSchemeActivity` | `CryptLinkDecryptor` + протокол | OK |
| Smart connect / ping | `MainActivity` + proxy GET | `VpnEngine` smart mode | Проверить паритет GET ping |
| Подписки 30 мин | `SubscriptionUpdater` | Таймер в Infrastructure | Сверить интервал |
| Продление подписки UI | `SubscriptionRenewalReminder` | `HomeView` + `SubscriptionRenewalHelper` | OK (0–7 дн.) |
| Mux/fragment defaults | `SettingsManager` off | Проверить defaults в PC | Синхронизировать |
| FCM push | Firebase опционально | N/A | — |
| Play / GitHub update | `UpdateCheckerManager` | `GitHubReleaseUpdateService` при старте | OK (PC repo) |
| UI | Material home + grouped list | WPF Cyber themes | Отдельный UX, функции важнее |

## Платформы Windows

| Сборка | ОС | RAM | Скрипт |
|--------|-----|-----|--------|
| **Full x64/x86** | Windows 10/11 | 2 ГБ+ | `build-all-platforms.ps1` |
| **Lite x64** | Windows 10/11 | 1 ГБ+ (нужен [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)) | `-Lite` |
| **Win 7/8.1** | SP1 + обновления | 1–2 ГБ | `build-legacy-win7.ps1` → **net6.0-windows7.0** + .NET 6 Desktop Runtime |

## Структура каталога

- Android: `D:\Sumrak\v2rayNG-master\v2rayNG-master`
- Desktop: `D:\Sumrak\v2rayN-master\v2rayN-master\SumraX`
- Upstream v2rayN (справка): `...\v2rayN\`

## Команда «всё на десктоп»

```powershell
cd D:\Sumrak\v2rayN-master\v2rayN-master\SumraX
.\scripts\build-all-platforms.ps1 -BuildInstaller
# Артефакты: publish\, dist\, dist-lite\, publish-x86\
```