# Sumrak VPN — Installation

## Ready-to-run (recommended)

After `.\scripts\build-all.ps1`:

```
SumraX\publish\Sumrak VPN.exe     ← launch this
SumraX\publish\bin\xray\            ← Xray-core (bundled, no setup needed)
```

**Total size ~163 MB** — fully offline after build. Internet only for VPN connection.

## Production build

```powershell
cd SumraX
.\scripts\build-all.ps1
```

Optional Windows installer (requires [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```powershell
.\scripts\build-all.ps1 -BuildInstaller
# Output: dist\SumraX-VPN-Setup-1.0.0.exe
```

## Bundled runtime

Located at `SumraX/runtime/xray/` in source tree:

| File | Size | Purpose |
|------|------|---------|
| xray.exe | ~34 MB | VPN engine |
| geoip.dat | ~19 MB | IP geo routing |
| geosite.dat | ~10 MB | Domain rules |
| wintun.dll | ~0.4 MB | TUN driver (future) |

Auto-copied to `bin\xray\` next to the app on every build.

Refresh runtime:

```powershell
.\scripts\download-xray.ps1
```

## First use

1. Run `Sumrak VPN.exe`
2. **Servers** → import config (Link / QR / File / Manual)
3. Click **Use** on a server
4. **Home** → **Connect**

## Data folder

```
%AppData%\Sumrak VPN\
  sumrax.db       — encrypted servers
  settings.json   — encrypted settings
  logs\           — application logs
  backups\        — setting backups
```

## Requirements

- Windows 10/11 x64
- No .NET install needed (self-contained exe)
