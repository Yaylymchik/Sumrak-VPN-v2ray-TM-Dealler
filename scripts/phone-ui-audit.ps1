# Сверка UI на телефоне: MTP off, скрин, дамп (если idle ok)
$ErrorActionPreference = "Continue"
$Out = Split-Path -Parent $PSScriptRoot

function Wait-Device {
    for ($i = 0; $i -lt 30; $i++) {
        adb reconnect 2>$null | Out-Null
        Start-Sleep 1
        if ((adb devices 2>$null) -match "`tdevice`$") { return $true }
    }
    return $false
}

if (-not (Wait-Device)) { throw "ADB: нет устройства" }

adb shell input keyevent KEYCODE_WAKEUP | Out-Null
adb shell settings put global window_animation_scale 0 | Out-Null
adb shell settings put global transition_animation_scale 0 | Out-Null
adb shell settings put global animator_duration_scale 0 | Out-Null

$ts = Get-Date -Format "yyyyMMdd_HHmmss"
$png = Join-Path $Out "ui_audit_$ts.png"
adb shell screencap -p /sdcard/audit.png
adb pull /sdcard/audit.png $png
Write-Host "Screenshot: $png"

$focus = adb shell dumpsys window 2>$null | Select-String "mCurrentFocus"
Write-Host $focus

$xml = Join-Path $Out "ui_audit_$ts.xml"
adb shell uiautomator dump /sdcard/audit.xml 2>$null
if ($LASTEXITCODE -eq 0) {
    adb pull /sdcard/audit.xml $xml
    Write-Host "UI dump: $xml"
} else {
    Write-Host "uiautomator dump failed (idle state / анимации). Используйте скриншот."
}