# One-shot: download all macOS offline runtimes (both Apple Silicon + Intel).
# Run once while online. After this, build on Mac without internet.
param(
    [ValidateSet("arm64", "x64", "both")]
    [string]$Arch = "both"
)

$ErrorActionPreference = "Stop"
& (Join-Path $PSScriptRoot "download-mac-runtimes.ps1") -Arch $Arch

Write-Host ""
Write-Host "Next step ON A MAC:" -ForegroundColor Cyan
Write-Host "  cd SumraX"
Write-Host "  chmod +x scripts/build-mac-offline.sh"
Write-Host "  ./scripts/build-mac-offline.sh"
Write-Host ""
Write-Host "Output: dist-mac/SumraX-VPN-macOS-<arch>-offline.zip" -ForegroundColor Green
