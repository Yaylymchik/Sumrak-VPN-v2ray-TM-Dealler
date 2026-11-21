# Download full macOS runtimes (xray + sing-box + geo) for offline Mac builds.
# Run once while online on Windows or Mac. Supports both Apple Silicon and Intel.
param(
    [ValidateSet("arm64", "x64", "both")]
    [string]$Arch = "both",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Headers = @{ "User-Agent" = "SumraX-VPN-Offline-Setup" }

function Get-LatestAssetUrl {
    param(
        [string]$Repo,
        [string[]]$Patterns,
        [string[]]$Extensions = @(".zip", ".tar.gz")
    )
    $json = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases/latest" -Headers $Headers
    foreach ($asset in $json.assets) {
        $name = $asset.name.ToLowerInvariant()
        foreach ($pattern in $Patterns) {
            if ($name -like "*$($pattern.ToLowerInvariant())*" -and ($Extensions | Where-Object { $name.EndsWith($_) })) {
                return @{ Url = $asset.browser_download_url; Name = $asset.name }
            }
        }
    }
    return $null
}

function Expand-ArchiveToDir {
    param([string]$ArchivePath, [string]$DestDir)
    New-Item -ItemType Directory -Force -Path $DestDir | Out-Null
    if ($ArchivePath.EndsWith(".zip")) {
        Expand-Archive -Path $ArchivePath -DestinationPath $DestDir -Force
        return
    }
    if ($ArchivePath.EndsWith(".tar.gz")) {
        tar -xzf $ArchivePath -C $DestDir
        return
    }
    throw "Unsupported archive: $ArchivePath"
}

function Flatten-Dir {
    param([string]$Dir)
    Get-ChildItem -Path $Dir -Recurse -File | ForEach-Object {
        $target = Join-Path $Dir $_.Name
        if ($_.FullName -ne $target) {
            Copy-Item $_.FullName $target -Force
        }
    }
}

function Install-XrayMac {
    param([string]$MacArch, [string]$TargetDir)
    $pattern = if ($MacArch -eq "arm64") { "macos-arm64" } else { "macos-64" }
    $xrayBin = Join-Path $TargetDir "xray"
    if ((Test-Path $xrayBin) -and -not $Force) {
        Write-Host "  xray ($MacArch): already present" -ForegroundColor Green
        return
    }

    Write-Host "  Downloading Xray ($pattern)..." -ForegroundColor Cyan
    $asset = Get-LatestAssetUrl -Repo "XTLS/Xray-core" -Patterns @($pattern) -Extensions @(".zip")
    if (-not $asset) { throw "Xray asset not found for $pattern" }

    $zip = Join-Path $env:TEMP "sumrax-xray-$MacArch.zip"
    Invoke-WebRequest -Uri $asset.Url -OutFile $zip -Headers $Headers -UseBasicParsing
    if (Test-Path $TargetDir) { Remove-Item $TargetDir -Recurse -Force }
    Expand-ArchiveToDir -ArchivePath $zip -DestDir $TargetDir
    Flatten-Dir -Dir $TargetDir
    Remove-Item $zip -Force

    if (-not (Test-Path $xrayBin)) { throw "xray binary missing after extract: $TargetDir" }
    Write-Host "  OK: $xrayBin" -ForegroundColor Green
}

function Install-SingBoxMac {
    param([string]$MacArch, [string]$TargetDir)
    $pattern = if ($MacArch -eq "arm64") { "darwin-arm64" } else { "darwin-amd64" }
    $bin = Join-Path $TargetDir "sing-box"
    if ((Test-Path $bin) -and -not $Force) {
        Write-Host "  sing-box ($MacArch): already present" -ForegroundColor Green
        return
    }

    Write-Host "  Downloading sing-box ($pattern)..." -ForegroundColor Cyan
    $asset = Get-LatestAssetUrl -Repo "SagerNet/sing-box" -Patterns @($pattern)
    if (-not $asset) { throw "sing-box asset not found for $pattern" }

    $archive = Join-Path $env:TEMP "sumrax-singbox-$MacArch$([IO.Path]::GetExtension($asset.Name))"
    if ($asset.Name.EndsWith(".tar.gz")) { $archive = Join-Path $env:TEMP "sumrax-singbox-$MacArch.tar.gz" }
    Invoke-WebRequest -Uri $asset.Url -OutFile $archive -Headers $Headers -UseBasicParsing
    if (Test-Path $TargetDir) { Remove-Item $TargetDir -Recurse -Force }
    Expand-ArchiveToDir -ArchivePath $archive -DestDir $TargetDir
    Flatten-Dir -Dir $TargetDir
    Remove-Item $archive -Force

    $found = Get-ChildItem $TargetDir -Recurse -File | Where-Object { $_.Name -eq "sing-box" } | Select-Object -First 1
    if ($found -and $found.FullName -ne $bin) {
        Copy-Item $found.FullName $bin -Force
    }
    if (-not (Test-Path $bin)) { throw "sing-box binary missing after extract: $TargetDir" }
    Write-Host "  OK: $bin" -ForegroundColor Green
}

function Copy-GeoFromWindows {
    param([string]$XrayDir)
    $winGeo = Join-Path $Root "runtime\xray"
    foreach ($file in @("geoip.dat", "geosite.dat", "LICENSE")) {
        $src = Join-Path $winGeo $file
        if (Test-Path $src) {
            Copy-Item $src (Join-Path $XrayDir $file) -Force
        }
    }
}

Write-Host "=== SumraX macOS Runtime Download (offline bundle prep) ===" -ForegroundColor Cyan

$archs = switch ($Arch) {
    "arm64" { @("arm64") }
    "x64"   { @("x64") }
    default { @("arm64", "x64") }
}

foreach ($a in $archs) {
    $base = Join-Path $Root "runtime\mac-$a"
    $xrayDir = Join-Path $base "xray"
    $singDir = Join-Path $base "sing-box"
    Write-Host ""
    Write-Host "[$a]" -ForegroundColor Yellow
    Install-XrayMac -MacArch $a -TargetDir $xrayDir
    Copy-GeoFromWindows -XrayDir $xrayDir
    Install-SingBoxMac -MacArch $a -TargetDir $singDir
}

Write-Host ""
Write-Host "=== DONE ===" -ForegroundColor Green
Write-Host "Run on Mac: ./scripts/build-mac-offline.sh"
Get-ChildItem (Join-Path $Root "runtime") -Directory -Filter "mac-*" | ForEach-Object {
    $mb = [math]::Round((Get-ChildItem $_.FullName -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
    Write-Host "  $($_.Name): $mb MB"
}
