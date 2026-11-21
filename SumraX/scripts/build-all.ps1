# Sumrak VPN - Full production build
param(
    [string]$Configuration = "Release",
    [switch]$SkipXrayDownload,
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$RuntimeDir = Join-Path $Root "runtime\xray"
$SingBoxDir = Join-Path $Root "runtime\sing-box"

Write-Host "=== Sumrak VPN Production Build ===" -ForegroundColor Cyan

if (-not $SkipXrayDownload) {
    & (Join-Path $PSScriptRoot "download-xray.ps1") -TargetDir $RuntimeDir
    & (Join-Path $PSScriptRoot "download-singbox.ps1") -TargetDir $SingBoxDir
}

& (Join-Path $PSScriptRoot "download-tun2.ps1")

& (Join-Path $PSScriptRoot "create-icon.ps1")
& (Join-Path $PSScriptRoot "publish.ps1") -Configuration $Configuration -SkipXrayDownload

if (Test-Path $RuntimeDir) {
    $pubXray = Join-Path $Root "publish\bin\xray"
    New-Item -ItemType Directory -Force -Path $pubXray | Out-Null
    Copy-Item "$RuntimeDir\*" $pubXray -Recurse -Force
}

if (Test-Path (Join-Path $Root "runtime\tun2\tun2proxy-bin.exe")) {
    $pubTun2 = Join-Path $Root "publish\bin\tun2"
    New-Item -ItemType Directory -Force -Path $pubTun2 | Out-Null
    Copy-Item "$Root\runtime\tun2\*" $pubTun2 -Recurse -Force
    Write-Host "Bundled tun2proxy (legacy, unused)" -ForegroundColor DarkGray
}

if (Test-Path (Join-Path $SingBoxDir "sing-box.exe")) {
    $pubSingBox = Join-Path $Root "publish\bin\sing-box"
    New-Item -ItemType Directory -Force -Path $pubSingBox | Out-Null
    Copy-Item "$SingBoxDir\*" $pubSingBox -Recurse -Force
    $wintun = Join-Path $RuntimeDir "wintun.dll"
    if ((Test-Path $wintun) -and -not (Test-Path (Join-Path $pubSingBox "wintun.dll"))) {
        Copy-Item $wintun (Join-Path $pubSingBox "wintun.dll") -Force
        Write-Host "Copied wintun.dll to sing-box bundle" -ForegroundColor Green
    }
    Write-Host "Bundled sing-box for TUIC/SSR/TUN" -ForegroundColor Green
}

# Remove PowerShell wrappers that trigger AV heuristics
Get-ChildItem (Join-Path $Root "publish") -Filter "*.ps1" -Recurse -ErrorAction SilentlyContinue |
    ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "Removed AV-sensitive script: $($_.Name)" -ForegroundColor DarkYellow
    }

# Generate SHA-256 integrity manifest for bundled cores
$integrity = @{ files = @{} }
$coreFiles = @(
    (Join-Path $Root "publish\bin\xray\xray.exe"),
    (Join-Path $Root "publish\bin\sing-box\sing-box.exe"),
    (Join-Path $Root "publish\bin\tun2\tun2proxy-bin.exe")
)
foreach ($file in $coreFiles) {
    if (Test-Path $file) {
        $hash = (Get-FileHash -Path $file -Algorithm SHA256).Hash
        $rel = $file.Replace((Join-Path $Root "publish") + [IO.Path]::DirectorySeparatorChar, "").Replace('\', '/')
        $integrity.files[$rel] = $hash
        Write-Host "Integrity: $rel" -ForegroundColor DarkGray
    }
}
if ($integrity.files.Count -gt 0) {
    $manifestPath = Join-Path $Root "publish\bin\integrity.json"
    $integrity | ConvertTo-Json -Depth 4 | Set-Content -Path $manifestPath -Encoding UTF8
    Write-Host "integrity.json created" -ForegroundColor Green
}

if ($BuildInstaller) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if ($iscc) {
        Write-Host "Building installer..." -ForegroundColor Cyan
        & $iscc (Join-Path $Root "installer\SumraXSetup.iss")
        Write-Host "Installer output: $(Join-Path $Root 'dist')" -ForegroundColor Green
    }
    else {
        Write-Host "Inno Setup not found - skip installer" -ForegroundColor Yellow
    }
}

$totalMb = [math]::Round((Get-ChildItem (Join-Path $Root "publish") -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)

Write-Host ""
Write-Host "=== BUILD COMPLETE ===" -ForegroundColor Green
Write-Host "App:  $(Join-Path $Root 'publish\Sumrak VPN.exe')"
Write-Host "Core: $(Join-Path $Root 'publish\bin\xray\xray.exe')"
Write-Host "Core: $(Join-Path $Root 'publish\bin\tun2\tun2proxy-bin.exe')"
Write-Host "Size: $totalMb MB total"
