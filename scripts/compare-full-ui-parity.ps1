# Full screen compare: optional reference PC/APK + Sumrak APK + SumraX PC
param(
    [string]$Device = "kjtgvcd6a6zxqkyh",
    [string]$RefPcProcess = $env:SUMRAX_COMPARE_REF_PC_PROCESS,
    [string]$RefPcWindowMatch = $env:SUMRAX_COMPARE_REF_PC_TITLE,
    [string]$RefApkPackage = $env:SUMRAX_COMPARE_REF_APK,
    [string]$RefApkActivity = $env:SUMRAX_COMPARE_REF_ACTIVITY,
    [string]$SumrakPcProcess = "Sumrak VPN",
    [string]$OutDir = ""
)

$ErrorActionPreference = "Stop"
$Root = Split-Path $PSScriptRoot -Parent
$PcRoot = "D:\Sumrak\v2rayN-master\v2rayN-master\SumraX"
if (-not $OutDir) {
    $OutDir = Join-Path $Root "compare_full_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
}
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Cap-Device($name) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    adb -s $Device shell input keyevent KEYCODE_WAKEUP 2>&1 | Out-Null
    adb -s $Device shell screencap -p /sdcard/cmp.png 2>&1 | Out-Null
    adb -s $Device pull /sdcard/cmp.png (Join-Path $OutDir "$name.png") 2>&1 | Out-Null
    $ErrorActionPreference = $prev
    if (-not (Test-Path (Join-Path $OutDir "$name.png"))) { throw "Device capture failed: $name" }
    Write-Host "OK $name (device)"
}

function Cap-WinProcess($name, $processName, $windowMatch) {
    Add-Type -AssemblyName System.Drawing
    Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinCap2 {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int n);
  public struct RECT { public int L, T, R, B; }
}
"@
    $p = Get-Process -ErrorAction SilentlyContinue |
        Where-Object {
            $_.MainWindowHandle -ne [IntPtr]::Zero -and
            ($_.ProcessName -eq $processName -or $_.MainWindowTitle -match [regex]::Escape($windowMatch))
        } | Select-Object -First 1
    if (-not $p) { throw "Window not found: $processName ($windowMatch)" }
    $h = $p.MainWindowHandle
    [WinCap2]::ShowWindow($h, 3) | Out-Null
    [WinCap2]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 700
    $rect = New-Object WinCap2+RECT
    [WinCap2]::GetWindowRect($h, [ref]$rect) | Out-Null
    $w = $rect.R - $rect.L; $ht = $rect.B - $rect.T
    $bmp = New-Object System.Drawing.Bitmap $w, $ht
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.L, $rect.T, 0, 0, $bmp.Size)
    $path = Join-Path $OutDir "$name.png"
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    Write-Host "OK $name (${w}x${ht})"
}

$dev = adb devices 2>&1 | Out-String
if ($dev -notmatch "${Device}\s+device") { throw "ADB device missing: $Device" }

if ($RefPcProcess -and $RefPcWindowMatch) {
    Cap-WinProcess "ref_pc_main" $RefPcProcess $RefPcWindowMatch
}

if ($RefApkPackage -and $RefApkActivity) {
    adb -s $Device shell am force-stop $RefApkPackage | Out-Null
    Start-Sleep -Milliseconds 350
    adb -s $Device shell am start -n "${RefApkPackage}/${RefApkActivity}" | Out-Null
    Start-Sleep -Seconds 3
    Cap-Device "ref_apk_main"
}

adb -s $Device shell am force-stop com.sumrax.app | Out-Null
Start-Sleep -Milliseconds 350
adb -s $Device shell am start -n com.sumrax.app/com.v2ray.ang.ui.MainActivity | Out-Null
Start-Sleep -Seconds 3
Cap-Device "sumrak_apk_main"
adb -s $Device shell input tap 540 1900
Start-Sleep -Seconds 2
Cap-Device "sumrak_apk_picker"

if (-not (Get-Process -Name $SumrakPcProcess -ErrorAction SilentlyContinue)) {
    $exe = Join-Path $PcRoot "src\SumraX.App\bin\Release\net8.0-windows10.0.19041.0\Sumrak VPN.exe"
    if (Test-Path $exe) { Start-Process $exe | Out-Null; Start-Sleep -Seconds 5 }
}
Cap-WinProcess "sumrak_pc_main" "Sumrak VPN" $SumrakPcProcess

$report = @"
# Full compare: reference (optional), Sumrak APK, Sumrak PC

Output: $OutDir

| File | |
|------|---|
| ref_pc_main.png | optional (`SUMRAX_COMPARE_REF_PC_*`) |
| ref_apk_main.png | optional (`SUMRAX_COMPARE_REF_APK`) |
| sumrak_apk_main.png | Sumrak VPN Android |
| sumrak_apk_picker.png | Server sheet (approx tap) |
| sumrak_pc_main.png | Sumrak VPN desktop |

Icon/UI: use sumrax-icon-48.png in sidebar (crisp 22px).
"@
Set-Content (Join-Path $OutDir "FULL_COMPARE.md") -Value $report -Encoding UTF8
Write-Host "OUT=$OutDir" -ForegroundColor Cyan