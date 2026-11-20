param(
    [string]$Device = "kjtgvcd6a6zxqkyh",
    [string]$OutDir = ""
)
if (-not $OutDir) {
    $OutDir = Join-Path (Split-Path $PSScriptRoot -Parent) "compare_ui_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Cap($name) {
    adb -s $Device shell screencap -p /sdcard/cmp.png | Out-Null
    adb -s $Device pull /sdcard/cmp.png (Join-Path $OutDir "$name.png") | Out-Null
    adb -s $Device shell uiautomator dump /sdcard/ui.xml 2>$null | Out-Null
    adb -s $Device pull /sdcard/ui.xml (Join-Path $OutDir "$name.xml") 2>$null | Out-Null
    Write-Host "OK $name"
}

adb -s $Device shell input keyevent KEYCODE_WAKEUP | Out-Null

# Happ
adb -s $Device shell am force-stop su.happ.proxyutility
Start-Sleep -Milliseconds 500
adb -s $Device shell am start -n su.happ.proxyutility/.feature.main.MainActivity | Out-Null
Start-Sleep -Seconds 3
Cap "happ_main"
# settings (gear left in toolbar)
adb -s $Device shell input tap 83 180
Start-Sleep -Seconds 2
Cap "happ_settings"
adb -s $Device shell input keyevent KEYCODE_BACK
Start-Sleep -Milliseconds 800
# add / servers
adb -s $Device shell input tap 998 180
Start-Sleep -Seconds 2
Cap "happ_server_screen"
adb -s $Device shell input keyevent KEYCODE_BACK
Start-Sleep -Milliseconds 800

# SumraX
adb -s $Device shell am force-stop com.sumrax.app
Start-Sleep -Milliseconds 500
adb -s $Device shell am start -n com.sumrax.app/com.v2ray.ang.ui.MainActivity | Out-Null
Start-Sleep -Seconds 2
Cap "sumrax_main"
adb -s $Device shell input tap 93 273
Start-Sleep -Seconds 2
Cap "sumrax_settings"
adb -s $Device shell input keyevent KEYCODE_BACK
Start-Sleep -Milliseconds 800
adb -s $Device shell input tap 981 1514
Start-Sleep -Seconds 1
Cap "sumrax_import_strip"
adb -s $Device shell input keyevent KEYCODE_BACK
Start-Sleep -Milliseconds 500
adb -s $Device shell input tap 93 273
Start-Sleep -Seconds 2
Cap "sumrax_menu"

Write-Host "OUT=$OutDir"