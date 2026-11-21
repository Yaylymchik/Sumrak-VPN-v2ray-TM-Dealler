# SumraX Desktop — parity with Sumrak APK

## Reference stack

| Layer | Source |
|-------|--------|
| Reference APK | Device screenshots (`ref_apk_main.png` in compare runs) |
| Sumrak phone | `jumpjump_home.xml`, `SumrakPowerButtonView.kt` |
| Reference PC | Optional — `tools/ref-desktop/` |
| SumraX PC | `HomeView.xaml`, `SumrakPowerButton.xaml`, `ServerPickerOverlay.xaml` |

## Screen compare script

```powershell
cd D:\Sumrak\v2rayNG-master\v2rayNG-master
.\scripts\compare-apk-desktop-screen.ps1
```

Output: `compare_ui_desktop_apk_YYYYMMDD_HHMMSS/` + `SCREEN_COMPARE_REPORT.md`.

## Home redesign (2.2.21+)

- Toolbar: **Settings · Smart/Manual · +**
- Center: **power ring** (`SumrakPowerButton`)
- Compact stats under status (ping / download)
- Empty state: AI + hint + Paste / QR (like APK)
- Server bar when `HasServers`
- Footer **YayLym · Sumrak**
- Sidebar accent: gold (`#FFD400`)

## Still different from APK

- Light grouped list on phone vs dark Sumrak design
- PC keeps sidebar + Speed/Security pages
- Proxy/TUN toggle on home (desktop-only convenience)

## Next (optional)

- Grouped server list on home (`rv_config_groups`)
- Screenshot PC picker in compare script