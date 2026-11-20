# SumraX UI audit: tap each control + screenshot (1080x2400)
$ErrorActionPreference = "Stop"
$Out = Join-Path (Split-Path $PSScriptRoot -Parent) "ui_redesign_audit"
New-Item -ItemType Directory -Force -Path $Out | Out-Null

function Wait-Device {
    adb kill-server 2>$null | Out-Null
    Start-Sleep 1
    adb start-server 2>$null | Out-Null
    for ($i = 0; $i -lt 20; $i++) {
        if ((adb devices 2>$null) -match "`tdevice`$") { return $true }
        Start-Sleep 1
    }
    return $false
}

function Shot($name) {
    $path = Join-Path $Out "$name.png"
    adb shell input keyevent KEYCODE_WAKEUP | Out-Null
    adb shell screencap -p /sdcard/_audit.png | Out-Null
    adb pull /sdcard/_audit.png $path | Out-Null
    Write-Host "  -> $path"
}

function Tap($x, $y) { adb shell input tap $x $y | Out-Null }

function Back { adb shell input keyevent KEYCODE_BACK | Out-Null; Start-Sleep 400 }

function HomeApp {
    adb shell am force-stop com.sumrax.app | Out-Null
    Start-Sleep 500
    adb shell monkey -p com.sumrax.app -c android.intent.category.LAUNCHER 1 | Out-Null
    Start-Sleep 2500
    adb shell settings put global window_animation_scale 0 | Out-Null
    adb shell settings put global transition_animation_scale 0 | Out-Null
    adb shell settings put global animator_duration_scale 0 | Out-Null
}

if (-not (Wait-Device)) {
    throw "ADB: no device. Connect phone with USB debugging and retry."
}

HomeApp
Shot "00_home_baseline"

Tap 77 175; Start-Sleep 800; Shot "01_nav_drawer_open"
Tap 400 1200; Start-Sleep 500; Shot "02_after_drawer_tap_outside"

Tap 771 175; Start-Sleep 600; Shot "03_toolbar_search"
Back; Start-Sleep 400

Tap 903 175; Start-Sleep 700; Shot "04_toolbar_add_submenu"
Back; Start-Sleep 400

Tap 1024 175; Start-Sleep 700; Shot "05_toolbar_overflow_menu"
Back; Start-Sleep 400

Tap 540 639; Start-Sleep 1500; Shot "06_power_button"
Tap 540 1200; Start-Sleep 300
Back; Start-Sleep 500
Shot "07_after_power_back"

HomeApp
Start-Sleep 500

Tap 448 1207; Start-Sleep 500; Shot "08_mode_manual"
Tap 184 1207; Start-Sleep 500; Shot "09_mode_smart"
Tap 739 1207; Start-Sleep 500; Shot "10_mode_proxy"
Tap 907 1207; Start-Sleep 500; Shot "11_mode_vpn"

Tap 529 1333; Start-Sleep 600; Shot "12_hwid_off"
Tap 672 1333; Start-Sleep 600; Shot "13_hwid_on"

Tap 761 1473; Start-Sleep 2500; Shot "14_servers_ping"
Tap 871 1473; Start-Sleep 3500; Shot "15_servers_refresh"
Tap 981 1473; Start-Sleep 1200; Shot "16_servers_import_overlay"

Tap 180 1750; Start-Sleep 400; Shot "17_picker_chip_favorites"
Tap 540 1750; Start-Sleep 400; Shot "18_picker_chip_recent"
Tap 180 1680; Start-Sleep 400; Shot "19_picker_chip_places"

Tap 1020 2050; Start-Sleep 800; Shot "20_picker_closed"

Tap 500 1616; Start-Sleep 500; Shot "21_home_chip_favorites"
Tap 840 1616; Start-Sleep 500; Shot "22_home_chip_recent"
Tap 180 1616; Start-Sleep 500; Shot "23_home_chip_places"

HomeApp
Start-Sleep 500
Tap 77 175; Start-Sleep 700
Tap 200 900; Start-Sleep 1500; Shot "24_drawer_item_tap"
Back; Start-Sleep 400

Write-Host "Done: $Out"
Get-ChildItem $Out -Filter *.png | Sort-Object Name | ForEach-Object { $_.Name }