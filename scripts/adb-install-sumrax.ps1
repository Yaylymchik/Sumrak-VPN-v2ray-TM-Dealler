# SumraX — пошаговая установка через ADB (arm64)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$V2 = Join-Path $Root "V2rayNG"
Set-Location $V2

Write-Host "[1/4] ADB device..."
adb devices
adb wait-for-device

Write-Host "[2/4] Build APK..."
.\gradlew.bat assemblePlaystoreDebug -PABI_FILTERS=arm64-v8a --no-daemon

$apk = Get-ChildItem -Path "app\build\outputs\apk\playstore\debug" -Filter "*arm64-v8a*.apk" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $apk) {
    $apk = Get-ChildItem -Path "app\build\outputs\apk\playstore\debug" -Filter "*.apk" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
}
if (-not $apk) { throw "APK not found under app\build\outputs\apk\playstore\debug" }

Write-Host "[3/4] Install $($apk.Name)..."
adb install -r $apk.FullName

Write-Host "[4/4] Launch com.sumrax.app..."
adb shell am start -n com.sumrax.app/com.v2ray.ang.ui.MainActivity
Write-Host "Done."