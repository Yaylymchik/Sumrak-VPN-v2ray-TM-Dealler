# Downloads Xray-core for Windows x64 into bundled runtime folder
param(
    [string]$TargetDir = (Join-Path $PSScriptRoot "..\runtime\xray"),
    [switch]$Force
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

$required = @("xray.exe", "geoip.dat", "geosite.dat")
$ready = ($required | ForEach-Object { Test-Path (Join-Path $TargetDir $_) }) -notcontains $false
if ($ready -and -not $Force) {
    Write-Host "Xray runtime already present in $TargetDir" -ForegroundColor Green
    exit 0
}

Write-Host "Fetching latest Xray-core release..." -ForegroundColor Cyan
$headers = @{ "User-Agent" = "SumraX-VPN-Setup" }
$release = Invoke-RestMethod -Uri "https://api.github.com/repos/XTLS/Xray-core/releases/latest" -Headers $headers
$asset = $release.assets | Where-Object { $_.name -match "windows-64.*\.zip$" } | Select-Object -First 1

if (-not $asset) {
    throw "windows-64 zip not found in latest Xray release"
}

$zipPath = Join-Path $env:TEMP "xray-core-$([guid]::NewGuid().ToString('n')).zip"
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -Headers $headers

$extractTemp = Join-Path $env:TEMP "xray-extract-$([guid]::NewGuid().ToString('n'))"
New-Item -ItemType Directory -Force -Path $extractTemp | Out-Null
Expand-Archive -Path $zipPath -DestinationPath $extractTemp -Force
Remove-Item $zipPath -Force

# Flatten: copy all files from extract (including nested) to target
Get-ChildItem -Path $extractTemp -Recurse -File | ForEach-Object {
    $dest = Join-Path $TargetDir $_.Name
    Copy-Item $_.FullName $dest -Force
}

Remove-Item $extractTemp -Recurse -Force

if (-not (Test-Path (Join-Path $TargetDir "xray.exe"))) {
    throw "xray.exe not found after extraction"
}

Write-Host "Xray runtime installed to $TargetDir" -ForegroundColor Green
Get-ChildItem $TargetDir | Format-Table Name, @{N='MB';E={[math]::Round($_.Length/1MB,2)}}
