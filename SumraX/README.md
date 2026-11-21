# Sumrak VPN (PC)

Windows VPN client built on **Xray / sing-box** with a modern WPF UI (**SumraX**).

Repository: [Yaylymchik/Sumrak-VPN-v2ray-PC](https://github.com/Yaylymchik/Sumrak-VPN-v2ray-PC)

## Features

- Protocols: VMess, VLESS, Trojan, Shadowsocks (+ sing-box for TUIC / advanced TUN)
- Import: link, clipboard, QR, file, manual profile
- Smart connect (best ping) and Manual mode
- System proxy + TUN (tun2proxy / WinTun)
- Kill Switch, DNS options, subscription refresh with profile purge
- Multi-theme UI (Cyber, Midnight, Emerald, Ember, Arctic)
- Localization: Russian / English / Turkmen
- SumraX AI assistant (diagnostics & auto-repair)
- Self-contained installer (no .NET install required on target PC)

## Requirements

| Target | OS | CPU | Install |
|--------|-----|-----|---------|
| Full (offline) | Windows 10/11 | x64, x86 | Self-contained, no .NET needed |
| Lite | Windows 10/11 | x64 | Needs [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) |
| Win 7/8.1 | SP1 | x64/x86 | See [docs/WINDOWS_LEGACY.md](docs/WINDOWS_LEGACY.md) (.NET 6 legacy) |

Build: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) · Installer: [Inno Setup 6](https://jrsoftware.org/isinfo.php)

**Android parity:** [docs/ANDROID_DESKTOP_PARITY.md](docs/ANDROID_DESKTOP_PARITY.md)

## Quick start (developer)

```powershell
cd Sumrak-VPN-v2ray-PC   # or SumraX folder
.\scripts\build-all.ps1
.\publish\"Sumrak VPN.exe"
```

First-time core download (required after clone):

```powershell
.\scripts\download-xray.ps1
.\scripts\download-singbox.ps1
.\scripts\download-tun2.ps1
```

Runtime binaries are **not** stored in git (size / license). Use the scripts above.
## Production / installer

```powershell
.\scripts\build-offline.ps1
# or all CPUs + weak-PC lite:
.\scripts\build-all-platforms.ps1 -Lite -BuildInstaller
```

Output:

| Artifact | Path |
|----------|------|
| Portable x64 | `publish\Sumrak VPN.exe` |
| Portable x86 | `publish-x86\Sumrak VPN.exe` |
| Lite x64 | `publish-lite\` (needs .NET 8 runtime) |
| Setup x64 | `dist\SumraX-VPN-Setup-*.exe` |
| Setup x86 | `dist\SumraX-VPN-Setup-*-x86.exe` |

## Project layout

```
├── src/                 # WPF app + Core + Infrastructure + Localization
├── tests/               # Unit tests
├── scripts/             # Build, publish, runtime download
├── installer/           # Inno Setup (SumraXSetup.iss)
├── runtime/             # Bundled cores (xray, sing-box, tun2)
├── docs/                # Architecture & install notes
└── publish/ · dist/     # Generated (gitignored)
```

## Docs

- [Installation](docs/INSTALLATION.md)
- [Architecture](docs/ARCHITECTURE.md)
- [macOS / High Sierra notes](docs/HIGH-SIERRA.md)

## Verify

```powershell
dotnet test .\SumraX.sln -c Release
.\scripts\verify-full.ps1
```

## License & credit

Coder **Sumrak** · Product UI **Sumrak VPN**.

Third-party cores (Xray, sing-box, tun2proxy, WinTun) remain under their respective upstream licenses.
