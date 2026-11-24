# Full desktop release: x64 + x86 + lite installers to D:\Sumrak
param(
    [switch]$SkipXrayDownload
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutRoot = "D:\Sumrak"

& (Join-Path $PSScriptRoot "sync-android-icon.ps1")
& (Join-Path $PSScriptRoot "download-dotnet-runtimes.ps1")

& (Join-Path $PSScriptRoot "build-all-platforms.ps1") -SkipXrayDownload:$SkipXrayDownload -Lite -BuildInstaller
& (Join-Path $PSScriptRoot "build-legacy-win7.ps1") -SkipXrayDownload:$SkipXrayDownload -BuildInstaller

$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
    & $iscc (Join-Path $Root "installer\SumraXSetup.iss")
    & $iscc (Join-Path $Root "installer\SumraXSetup-x86.iss")
    & $iscc (Join-Path $Root "installer\SumraXSetup-Lite.iss")
    & $iscc (Join-Path $Root "installer\SumraXSetup-Win7.iss")
    & $iscc (Join-Path $Root "installer\SumraXSetup-Win7-x86.iss")
    & (Join-Path $PSScriptRoot "build-smart-installer.ps1") -ForceRebuildAll:$false
}

$dist = Join-Path $Root "dist"
New-Item -ItemType Directory -Force -Path $OutRoot | Out-Null

$version = "2.2.21"
$publishNames = @(
    "SumraX-VPN-Setup-$version-Smart.exe",
    "SumraX-VPN-Setup-$version-Lite.exe"
)
foreach ($name in $publishNames) {
    $src = Join-Path $dist $name
    if (-not (Test-Path $src)) {
        Write-Warning "Missing dist payload (build only): $name"
        continue
    }
    $dest = Join-Path $OutRoot $name
    Copy-Item $src $dest -Force
    Write-Host "Copied installer: $dest" -ForegroundColor Green
}

& (Join-Path $PSScriptRoot "prune-d-sumrak-vpn.ps1") -Version $version -OutRoot $OutRoot

$assetIcon = Join-Path $Root "src\SumraX.App\Assets\sumrax-icon.png"
$assetIco = Join-Path $Root "src\SumraX.App\Assets\sumrax.ico"
foreach ($asset in @($assetIcon, $assetIco)) {
    if (Test-Path $asset) {
        Copy-Item $asset (Join-Path $OutRoot (Split-Path $asset -Leaf)) -Force
        Write-Host "Copied branding: $(Join-Path $OutRoot (Split-Path $asset -Leaf))" -ForegroundColor Green
    }
}

& (Join-Path $PSScriptRoot "compare-android-desktop.ps1")

Write-Host ""
Write-Host "=== Ideal desktop release ready in $OutRoot ===" -ForegroundColor Cyan