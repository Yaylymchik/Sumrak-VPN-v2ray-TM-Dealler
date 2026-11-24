# Build Smart installer (bundles all platform-specific setups)
param(
    [switch]$ForceRebuildAll
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Dist = Join-Path $Root "dist"

$required = @(
    "SumraX-VPN-Setup-2.2.21.exe",
    "SumraX-VPN-Setup-2.2.21-x86.exe",
    "SumraX-VPN-Setup-2.2.21-Win7.exe",
    "SumraX-VPN-Setup-2.2.21-Win7-x86.exe"
)

$missing = @()
foreach ($name in $required) {
    if (-not (Test-Path (Join-Path $Dist $name))) { $missing += $name }
}

if ($missing.Count -gt 0 -or $ForceRebuildAll) {
    Write-Host "Building child installers (missing: $($missing -join ', '))..." -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot "sync-android-icon.ps1")
    & (Join-Path $PSScriptRoot "download-dotnet-runtimes.ps1")
    & (Join-Path $PSScriptRoot "build-all-platforms.ps1") -SkipXrayDownload -Lite -BuildInstaller
    & (Join-Path $PSScriptRoot "build-legacy-win7.ps1") -SkipXrayDownload -BuildInstaller
    $isccChild = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($isccChild) {
        & $isccChild (Join-Path $Root "installer\SumraXSetup.iss")
        & $isccChild (Join-Path $Root "installer\SumraXSetup-x86.iss")
        & $isccChild (Join-Path $Root "installer\SumraXSetup-Lite.iss")
        & $isccChild (Join-Path $Root "installer\SumraXSetup-Win7.iss")
        & $isccChild (Join-Path $Root "installer\SumraXSetup-Win7-x86.iss")
    }
}

foreach ($name in $required) {
    $p = Join-Path $Dist $name
    if (-not (Test-Path $p)) {
        throw "Required payload missing: $p"
    }
}

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $iscc) {
    throw "Inno Setup 6 not found"
}

$iss = Join-Path $Root "installer\SumraXSetup-Smart.iss"
& $iscc $iss

$smart = Join-Path $Dist "SumraX-VPN-Setup-2.2.21-Smart.exe"
if (Test-Path $smart) {
    $out = "D:\Sumrak\SumraX-VPN-Setup-2.2.21-Smart.exe"
    Copy-Item $smart $out -Force
    Write-Host "Smart installer ready: $out" -ForegroundColor Green
}