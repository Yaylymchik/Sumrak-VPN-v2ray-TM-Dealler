# Sumrak VPN - offline production build + installer
param(
    [string]$Configuration = "Release",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

Write-Host "=== Sumrak VPN OFFLINE BUILD ===" -ForegroundColor Cyan

if (-not (Test-Path (Join-Path $Root "runtime\xray\xray.exe"))) {
    throw "Offline build requires bundled xray at runtime\xray\xray.exe. Run download-xray.ps1 once while online."
}

$SingBoxRuntime = Join-Path $Root "runtime\sing-box\sing-box.exe"
if (-not (Test-Path $SingBoxRuntime)) {
    Write-Host "sing-box not found - downloading..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "download-singbox.ps1")
}
if (-not (Test-Path $SingBoxRuntime)) {
    throw "Offline build requires sing-box at runtime\sing-box\sing-box.exe"
}

& (Join-Path $PSScriptRoot "create-icon.ps1")
& (Join-Path $PSScriptRoot "build-all.ps1") -Configuration $Configuration -SkipXrayDownload -BuildInstaller:(-not $SkipInstaller)

$dist = Join-Path $Root "dist"
$setup = Get-ChildItem $dist -Filter "SumraX-VPN-Setup-*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($setup) {
    $sizeMb = [math]::Round($setup.Length / 1MB, 1)
    Write-Host ""
    Write-Host ("Installer: {0} ({1} MB)" -f $setup.FullName, $sizeMb) -ForegroundColor Green
}
elseif (-not $SkipInstaller) {
    Write-Host "WARNING: Installer not found in dist" -ForegroundColor Yellow
}
