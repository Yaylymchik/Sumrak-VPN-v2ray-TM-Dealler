# Sumrak VPN Publish Script
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [switch]$SkipXrayDownload
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$AppProj = Join-Path $Root "src\SumraX.App\SumraX.App.csproj"
$OutDir = Join-Path $Root "publish"
$RuntimeDir = Join-Path $Root "runtime\xray"

Write-Host "Publishing Sumrak VPN..." -ForegroundColor Cyan

if (-not $SkipXrayDownload) {
    & (Join-Path $PSScriptRoot "download-xray.ps1") -TargetDir $RuntimeDir
}

dotnet publish $AppProj `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $OutDir

Write-Host ""
Write-Host "Published to: $OutDir" -ForegroundColor Green

if (Test-Path (Join-Path $OutDir "bin\xray\xray.exe")) {
    Write-Host "Xray-core bundled: OK" -ForegroundColor Green
}
else {
    Write-Host "WARNING: xray.exe missing in publish output" -ForegroundColor Red
}

$version = "1.0.0"
try {
    $proj = [xml](Get-Content $AppProj)
    $verNode = $proj.Project.PropertyGroup.Version | Select-Object -First 1
    if ($verNode) { $version = $verNode }
} catch { }

$manifest = @{
    version     = $version
    downloadUrl = ""
    notes       = "Sumrak VPN $version"
} | ConvertTo-Json -Compress
Set-Content -Path (Join-Path $OutDir "version.json") -Value $manifest -Encoding UTF8
Write-Host "version.json: $version" -ForegroundColor Green
