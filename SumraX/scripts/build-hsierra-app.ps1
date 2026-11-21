# Build Sumrak VPN.app — same layout as Sumrak.macOS.universal.dmg
param(
    [string]$DestRoot = "d:\v2rayN-master\v2rayN-master\SumraX\dist-hsierra",
    [string]$RepoRoot = (Join-Path $PSScriptRoot "..")
)

$ErrorActionPreference = "Stop"
$RepoRoot = (Resolve-Path $RepoRoot).Path
$Hs = Join-Path $RepoRoot "scripts\hsierra"
$XraySrc = Join-Path $RepoRoot "runtime\mac-x64\xray"

if (-not (Test-Path "$XraySrc\xray")) {
    Write-Error "Missing xray at $XraySrc"
}

New-Item -ItemType Directory -Force -Path $DestRoot | Out-Null

$AppDir = Join-Path $DestRoot "Sumrak VPN.app"
$MacOS = Join-Path $AppDir "Contents\MacOS"
$Core = Join-Path $MacOS "core\xray"

if (Test-Path $AppDir) { Remove-Item $AppDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $Core, "$MacOS\config", "$MacOS\data", "$MacOS\scripts" | Out-Null

# Bundle layout: MacOS/core/xray, MacOS/config, launcher binary
Copy-Item "$XraySrc\*" $Core -Force
Copy-Item "$Hs\sumrax" $MacOS -Force
Copy-Item "$Hs\sumrax_gui.py" $MacOS -Force
Copy-Item "$RepoRoot\src\SumraX.Infrastructure\Resources\proxy_set_osx.sh" "$MacOS\scripts\" -Force
Copy-Item "$Hs\Info.plist" "$AppDir\Contents\" -Force
Copy-Item (Join-Path $Hs "macos-app-launcher.sh") (Join-Path $MacOS "Sumrak VPN") -Force

foreach ($cfg in @("E:\config.json", "$DestRoot\config\config.json", "$DestRoot\..\SumraX-HighSierra\config\config.json")) {
    if (Test-Path $cfg) {
        Copy-Item $cfg "$MacOS\config\config.json" -Force
        break
    }
}

# DMG stage like "v2.17.1" folder inside dmg
$DmgStage = Join-Path $DestRoot "Sumrak VPN 1.0"
if (Test-Path $DmgStage) { Remove-Item $DmgStage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $DmgStage | Out-Null
Copy-Item $AppDir $DmgStage -Recurse -Force
@(
    "Sumrak VPN - install like Sumrak.macOS.universal.dmg",
    "",
    "On Mac:",
    "  open SumraX-VPN-macOS-x64.dmg",
    "  drag Sumrak VPN.app to Applications",
    "",
    "Or copy Sumrak VPN.app from this folder directly.",
    "",
    "needs macOS 10.14+. SumraX works on 10.13 High Sierra.",
    "config.json is inside the app."
) | Set-Content (Join-Path $DmgStage "INSTALL.txt") -Encoding ASCII

Set-Content (Join-Path $DestRoot "INSTALL.txt") -Value (Get-Content (Join-Path $DmgStage "INSTALL.txt")) -Encoding ASCII

$mb = [math]::Round((Get-ChildItem $AppDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "App: $AppDir (${mb} MB)"
Write-Host "DMG stage: $DmgStage"
Write-Host "On Mac: hdiutil create -volname 'Sumrak VPN' -srcfolder '$DmgStage' -ov -format UDZO SumraX-VPN-macOS-x64.dmg"
