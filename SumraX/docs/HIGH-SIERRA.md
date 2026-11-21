# Build High Sierra CLI bundle on Mac (no .NET) or copy from Windows

From Windows, runtime is already in `runtime/mac-x64`. Copy whole `SumraX` folder to Mac, then:

```bash
chmod +x scripts/build-hsierra-offline.sh scripts/hsierra/sumrax
./scripts/build-hsierra-offline.sh
```

Output: `dist-hsierra/SumraX-VPN-HighSierra-x64.dmg`

## On High Sierra Mac

```bash
# After mounting/copying SumraX-VPN-CLI folder:
./sumrax import ~/Downloads/config.json   # from Windows SumraX
./sumrax start
./sumrax stop
```

Get `config.json` from Windows: `%AppData%\Sumrak VPN\config.json` (connect once on Windows first).
