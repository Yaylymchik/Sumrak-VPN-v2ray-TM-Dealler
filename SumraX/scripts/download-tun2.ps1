param(
    [string]$TargetDir = (Join-Path (Split-Path -Parent $PSScriptRoot) "runtime\tun2"),
    [string]$BundledTun2Dir = $env:SUMRAX_BUNDLED_TUN2_DIR
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

if (-not $BundledTun2Dir) {
    $BundledTun2Dir = "${env:ProgramFiles}\FlyFrogLLC\Sumrak VPN\tun2"
}
if (Test-Path (Join-Path $BundledTun2Dir "tun2proxy-bin.exe")) {
    Copy-Item "$BundledTun2Dir\tun2proxy-bin.exe", "$BundledTun2Dir\tun2proxy.dll", "$BundledTun2Dir\wintun.dll" $TargetDir -Force
    if (Test-Path (Join-Path $BundledTun2Dir "udpgw-server.exe")) {
        Copy-Item (Join-Path $BundledTun2Dir "udpgw-server.exe") $TargetDir -Force
    }
    Write-Host "Copied tun2proxy from ($BundledTun2Dir)" -ForegroundColor Green
    exit 0
}

$zipPath = Join-Path $env:TEMP "tun2proxy-win.zip"
$api = Invoke-RestMethod "https://api.github.com/repos/tun2proxy/tun2proxy/releases/latest"
$asset = $api.assets | Where-Object { $_.name -match "windows.*x86_64.*\.zip$" -or $_.name -match "win.*x64.*\.zip$" } | Select-Object -First 1
if (-not $asset) {
    $asset = $api.assets | Where-Object { $_.name -like "*windows*.zip" } | Select-Object -First 1
}
if (-not $asset) {
    throw "No Windows tun2proxy release asset found."
}

Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -UseBasicParsing
Expand-Archive -Path $zipPath -DestinationPath $TargetDir -Force
Get-ChildItem $TargetDir -Recurse -Filter "tun2proxy-bin.exe" | ForEach-Object {
    Copy-Item $_.FullName (Join-Path $TargetDir "tun2proxy-bin.exe") -Force
    if (Test-Path (Join-Path $_.DirectoryName "tun2proxy.dll")) {
        Copy-Item (Join-Path $_.DirectoryName "tun2proxy.dll") $TargetDir -Force
    }
}
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue

$xrayWintun = Join-Path (Split-Path -Parent $PSScriptRoot) "runtime\xray\wintun.dll"
if ((Test-Path $xrayWintun) -and -not (Test-Path (Join-Path $TargetDir "wintun.dll"))) {
    Copy-Item $xrayWintun (Join-Path $TargetDir "wintun.dll") -Force
}

if (-not (Test-Path (Join-Path $TargetDir "tun2proxy-bin.exe"))) {
    throw "tun2proxy-bin.exe not found after download."
}

Write-Host "Downloaded tun2proxy to $TargetDir" -ForegroundColor Green