# Real-screen capture: reference APK (optional) + Sumrak APK + SumraX Windows
param(
    [string]$Device = "kjtgvcd6a6zxqkyh",
    [string]$OutDir = "",
    [string]$RefApkPackage = $env:SUMRAX_COMPARE_REF_APK,
    [string]$RefApkActivity = $env:SUMRAX_COMPARE_REF_ACTIVITY
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$PcRoot = "D:\Sumrak\v2rayN-master\v2rayN-master\SumraX"
if (-not $OutDir) {
    $OutDir = Join-Path $Root "compare_ui_desktop_apk_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Cap-Device($name) {
    adb -s $Device shell input keyevent KEYCODE_WAKEUP | Out-Null
    adb -s $Device shell screencap -p /sdcard/cmp.png | Out-Null
    adb -s $Device pull /sdcard/cmp.png (Join-Path $OutDir "$name.png") | Out-Null
    try {
        adb -s $Device shell uiautomator dump /sdcard/ui.xml 2>&1 | Out-Null
        adb -s $Device pull /sdcard/ui.xml (Join-Path $OutDir "$name.xml") 2>&1 | Out-Null
    } catch { }
    Write-Host "OK $name"
}

function Cap-Desktop($name) {
    Add-Type -AssemblyName System.Drawing
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinCap {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  public struct RECT { public int L, T, R, B; }
}
"@
    $p = Get-Process -Name "Sumrak VPN" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p -or $p.MainWindowHandle -eq [IntPtr]::Zero) {
        throw "Sumrak VPN desktop is not running or has no main window"
    }
    $h = $p.MainWindowHandle
    [WinCap]::ShowWindow($h, 9) | Out-Null
    [WinCap]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 800
    $rect = New-Object WinCap+RECT
    [WinCap]::GetWindowRect($h, [ref]$rect) | Out-Null
    $w = $rect.R - $rect.L
    $ht = $rect.B - $rect.T
    if ($w -lt 100 -or $ht -lt 100) { throw "Desktop window too small; restore/maximize the app window" }
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.L, $rect.T, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir "$name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose()
    $bmp.Dispose()
    Write-Host "OK $name size=${w}x${ht}"
}

$devList = adb devices 2>&1 | Out-String
if ($devList -notmatch "${Device}\s+device") { throw "ADB device $Device not found" }

if ($RefApkPackage -and $RefApkActivity) {
    adb -s $Device shell am force-stop $RefApkPackage | Out-Null
    Start-Sleep -Milliseconds 400
    adb -s $Device shell am start -n "${RefApkPackage}/${RefApkActivity}" | Out-Null
    Start-Sleep -Seconds 3
    Cap-Device "ref_apk_main"
}

adb -s $Device shell am force-stop com.sumrax.app | Out-Null
Start-Sleep -Milliseconds 400
adb -s $Device shell am start -n com.sumrax.app/com.v2ray.ang.ui.MainActivity | Out-Null
Start-Sleep -Seconds 3
Cap-Device "sumrak_apk_main"
adb -s $Device shell input tap 981 1514
Start-Sleep -Seconds 2
Cap-Device "sumrak_apk_picker"
adb -s $Device shell input keyevent KEYCODE_BACK | Out-Null
Start-Sleep -Milliseconds 500
adb -s $Device shell input tap 771 175
Start-Sleep -Seconds 2
Cap-Device "sumrak_apk_settings"

$exe = Join-Path $PcRoot "publish\Sumrak VPN.exe"
if (-not (Get-Process -Name "Sumrak VPN" -ErrorAction SilentlyContinue)) {
    if (-not (Test-Path $exe)) { throw "Desktop exe missing: $exe" }
    Start-Process $exe | Out-Null
    Start-Sleep -Seconds 8
}
Cap-Desktop "sumrax_pc_main"

$refCopied = 0
$refDesk = Join-Path $PcRoot "tools\ref-desktop"
if (Test-Path $refDesk) {
    Get-ChildItem $refDesk -Filter "*.png" -Recurse -ErrorAction SilentlyContinue | ForEach-Object {
        $dest = Join-Path $OutDir "ref_desktop_$($_.BaseName).png"
        Copy-Item $_.FullName $dest -Force
        $refCopied++
    }
}

$refNote = if ($refCopied -gt 0) { "$refCopied PNG from tools/ref-desktop" } else { "tools/ref-desktop empty (compare Sumrak APK + PC only)" }

$reportPath = Join-Path $OutDir "SCREEN_COMPARE_REPORT.md"
@"
# Screen compare: Sumrak APK vs SumraX PC

Folder: $OutDir
Device: $Device
Time: $(Get-Date -Format 'yyyy-MM-dd HH:mm')

Files:
- ref_apk_main.png (optional, set SUMRAX_COMPARE_REF_APK)
- sumrak_apk_main.png
- sumrak_apk_picker.png
- sumrak_apk_settings.png
- sumrax_pc_main.png

ref-desktop refs: $refNote

Open PNG side by side: power ring, server sheet icon order (ping, refresh, import), settings hub sections.
"@ | Set-Content -Path $reportPath -Encoding UTF8

Write-Host "OUT=$OutDir" -ForegroundColor Cyan