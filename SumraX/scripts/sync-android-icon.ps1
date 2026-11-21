# Sync desktop/tray/installer icons from Android launcher (same brand as phone APK)
param(
    [string]$AndroidRes = "D:\Sumrak\v2rayNG-master\v2rayNG-master\V2rayNG\app\src\main\res"
)

$ErrorActionPreference = "Stop"
$candidates = @(
    (Join-Path $AndroidRes "mipmap-xxxhdpi\ic_launcher.png"),
    (Join-Path $AndroidRes "mipmap-xxhdpi\ic_launcher.png"),
    (Join-Path $AndroidRes "mipmap-xxxhdpi\ic_launcher_foreground.png")
)
$src = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $src) { throw "Android launcher PNG not found under $AndroidRes" }

& (Join-Path $PSScriptRoot "create-sumrak-icons.ps1") -SourcePng $src
Write-Host "Desktop icons synced from: $src" -ForegroundColor Green