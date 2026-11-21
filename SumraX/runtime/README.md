Bundled VPN cores for Sumrak VPN offline builds.

## Windows

| Path | Contents |
|------|----------|
| `xray/` | xray.exe, geoip.dat, geosite.dat, wintun.dll |
| `sing-box/` | sing-box.exe |

Refresh:

```powershell
.\scripts\download-xray.ps1
.\scripts\download-singbox.ps1
```

## macOS (offline bundle prep)

| Path | Contents |
|------|----------|
| `mac-arm64/xray/` | xray, geoip.dat, geosite.dat |
| `mac-arm64/sing-box/` | sing-box |
| `mac-x64/xray/` | xray, geoip.dat, geosite.dat |
| `mac-x64/sing-box/` | sing-box |

Download (once, while online — from Windows or Mac):

```powershell
.\scripts\prepare-mac-offline.ps1
```

Build `.app` on Mac:

```bash
./scripts/build-mac-offline.sh
```
