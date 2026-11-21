# Sumrak VPN — Architecture

## Overview

Sumrak VPN is a modular Windows desktop client built on **Clean Architecture** principles with direct **Xray-core** integration.

```
SumraX.App (WPF / MVVM)
    └── SumraX.Infrastructure (VPN engine, storage, security)
            └── SumraX.Core (domain models, interfaces)
            └── SumraX.Localization (i18n)
```

## Layers

| Layer | Responsibility |
|-------|----------------|
| **SumraX.Core** | Domain models, enums, service contracts |
| **SumraX.Infrastructure** | Xray process, config builder, DPAPI encryption, SQLite, updates |
| **SumraX.Localization** | ru / en / tk string resources |
| **SumraX.App** | Premium WPF UI, ViewModels (CommunityToolkit.Mvvm) |

## VPN Pipeline

1. User selects server → `VpnEngine.ConnectAsync`
2. `XrayConfigBuilder` generates `config.json`
3. `xray.exe run -c config.json` starts in `bin/xray/`
4. Windows system proxy set to local HTTP port
5. `NetworkMonitor` + auto-reconnect on process exit

## Security

- Server configs encrypted with **Windows DPAPI** (CurrentUser scope)
- Settings file encrypted at rest
- Kill Switch via Windows Firewall (`netsh advfirewall`)
- DNS leak protection routes port 53 through tunnel

## Extensibility

- Add protocols in `XrayConfigBuilder` + `ConfigImportService`
- Add languages in `LocalizationService.BuildStrings()`
- Plug external AI API in `AiAssistantService`
