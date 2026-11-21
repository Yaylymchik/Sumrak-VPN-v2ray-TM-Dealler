# Download sing-box for Windows (optional core for TUIC / SSR obfs)
param(
    [string]$TargetDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "runtime\sing-box"),
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$Release = "https://api.github.com/repos/SagerNet/sing-box/releases/latest"

Write-Host "Fetching latest sing-box release..." -ForegroundColor Cyan
if ($Force -and (Test-Path (Join-Path $TargetDir "sing-box.exe"))) {
    Remove-Item (Join-Path $TargetDir "sing-box.exe") -Force -ErrorAction SilentlyContinue
}
$json = Invoke-RestMethod -Uri $Release -Headers @{ "User-Agent" = "SumraX-Build" }
$asset = $json.assets | Where-Object { $_.name -match "windows-amd64\.zip$" } | Select-Object -First 1
if (-not $asset) {
    throw "sing-box windows-amd64.zip not found in latest release"
}

New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null
$zip = Join-Path $env:TEMP "sing-box-download.zip"
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing

Expand-Archive -Path $zip -DestinationPath $TargetDir -Force
Remove-Item $zip -Force

$exe = Get-ChildItem $TargetDir -Recurse -Filter "sing-box.exe" | Select-Object -First 1
if ($exe -and $exe.DirectoryName -ne $TargetDir) {
    Move-Item $exe.FullName (Join-Path $TargetDir "sing-box.exe") -Force
    Get-ChildItem $TargetDir -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path (Join-Path $TargetDir "sing-box.exe"))) {
    throw "sing-box.exe not found after extraction"
}

Write-Host "OK: $(Join-Path $TargetDir 'sing-box.exe')" -ForegroundColor Green
