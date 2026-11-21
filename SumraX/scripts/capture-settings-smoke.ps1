param(
    [string]$OutDir = (Join-Path (Split-Path $PSScriptRoot -Parent) "test-capture-settings")
)

$ErrorActionPreference = "Stop"
$exe = Join-Path (Split-Path $PSScriptRoot -Parent) "src\SumraX.App\bin\Release\net8.0-windows10.0.19041.0\Sumrak VPN.exe"
if (-not (Test-Path $exe)) { throw "Build first: $exe" }

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
Get-Process "Sumrak VPN" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep 1

$log = Join-Path $OutDir "smoke.log"
$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 6

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinCap3 {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int L, T, R, B; }
}
"@

$p = Get-Process -Id $proc.Id -ErrorAction SilentlyContinue
if (-not $p -or $p.MainWindowHandle -eq [IntPtr]::Zero) {
    Get-Content $log -ErrorAction SilentlyContinue | Set-Content (Join-Path $OutDir "FAIL.log")
    throw "Main window not shown"
}

$rect = New-Object WinCap3+RECT
[WinCap3]::GetWindowRect($p.MainWindowHandle, [ref]$rect) | Out-Null
$w = $rect.R - $rect.L; $h = $rect.B - $rect.T
$bmp = New-Object System.Drawing.Bitmap $w, $h
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($rect.L, $rect.T, 0, 0, $bmp.Size)
$png = Join-Path $OutDir "sumrak_startup.png"
$bmp.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()

"OK pid=$($proc.Id) size=${w}x${h}" | Set-Content (Join-Path $OutDir "RESULT.txt")
Write-Host "OUT=$OutDir"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue