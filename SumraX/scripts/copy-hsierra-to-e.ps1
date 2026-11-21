# Copy Sumrak bundle to flash E:
$Repo = Split-Path $PSScriptRoot -Parent
$Src = Join-Path $Repo "dist-hsierra"
$Dest = "E:\SumraX-HighSierra"

& (Join-Path $PSScriptRoot "build-hsierra-app.ps1") -DestRoot $Src | Out-Null

if (-not (Test-Path "E:\")) {
    Write-Error "Flash E: not found"
}

New-Item -ItemType Directory -Force -Path $Dest | Out-Null
Copy-Item "$Src\Sumrak VPN.app" $Dest -Recurse -Force
Copy-Item "$Src\Sumrak VPN 1.0" $Dest -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "$Src\INSTALL.txt" $Dest -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $Repo "docs\MAC-TERMINAL.txt") $Dest -Force -ErrorAction SilentlyContinue
Write-Host "E:\SumraX-HighSierra\Sumrak VPN.app ready"
