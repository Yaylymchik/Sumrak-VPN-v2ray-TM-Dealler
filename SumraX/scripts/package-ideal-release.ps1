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
    if ((Test-Path (Join-Path $Root "dist\SumraX-VPN-Setup-2.2.21-Win7.exe")) -and
        (Test-Path (Join-Path $Root "dist\SumraX-VPN-Setup-2.2.21-Win7-x86.exe"))) {
        & $iscc (Join-Path $Root "installer\SumraXSetup-Smart.iss")
    }
}

$dist = Join-Path $Root "dist"
New-Item -ItemType Directory -Force -Path $OutRoot | Out-Null

foreach ($f in Get-ChildItem $dist -Filter "SumraX-VPN-Setup-*.exe" -ErrorAction SilentlyContinue) {
    $dest = Join-Path $OutRoot $f.Name
    Copy-Item $f.FullName $dest -Force
    Write-Host "Copied installer: $dest" -ForegroundColor Green
}

$portable = Join-Path $Root "publish\Sumrak VPN.exe"
if (Test-Path $portable) {
    $dest = Join-Path $OutRoot "SumrakVPN_Desktop_2.2.21_x64_Portable.exe"
    Copy-Item $portable $dest -Force
    Write-Host "Copied portable: $dest" -ForegroundColor Green
}

$portableX86 = Join-Path $Root "publish-x86\Sumrak VPN.exe"
if (Test-Path $portableX86) {
    $dest = Join-Path $OutRoot "SumrakVPN_Desktop_2.2.21_x86_Portable.exe"
    Copy-Item $portableX86 $dest -Force
    Write-Host "Copied portable x86: $dest" -ForegroundColor Green
}

foreach ($pattern in @("*-Win7*.exe", "*-Lite.exe", "*-Smart.exe")) {
    foreach ($f in Get-ChildItem $dist -Filter $pattern -ErrorAction SilentlyContinue) {
        $dest = Join-Path $OutRoot $f.Name
        Copy-Item $f.FullName $dest -Force
        Write-Host "Copied installer: $dest" -ForegroundColor Green
    }
}

$win7ZipName = "SumrakVPN_Desktop_2.2.21_Win7_x64.zip"
$win7Pub = Join-Path $Root "publish-win7"
if (Test-Path $win7Pub) {
    $zipDest = Join-Path $OutRoot $win7ZipName
    if (Test-Path $zipDest) { Remove-Item $zipDest -Force }
    Compress-Archive -Path (Join-Path $win7Pub "*") -DestinationPath $zipDest
    Write-Host "Copied Win7 portable zip: $zipDest" -ForegroundColor Green
}

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