# Multi-arch + weak-PC publish for Sumrak VPN (desktop)
param(
    [string]$Configuration = "Release",
    [switch]$SkipXrayDownload,
    [switch]$BuildInstaller,
    [switch]$Lite
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

function Publish-Sumrak {
    param(
        [string]$Rid,
        [string]$OutDir,
        [switch]$FrameworkDependent
    )
    $AppProj = Join-Path $Root "src\SumraX.App\SumraX.App.csproj"
    $tfm = "net8.0-windows10.0.19041.0"
    if ($FrameworkDependent) {
        dotnet publish $AppProj -c $Configuration -f $tfm -r $Rid -o $OutDir `
            --self-contained false `
            -p:PublishSingleFile=false
    }
    else {
        dotnet publish $AppProj -c $Configuration -f $tfm -r $Rid -o $OutDir `
            --self-contained true `
            -p:PublishSingleFile=true `
            -p:IncludeNativeLibrariesForSelfExtract=true `
            -p:EnableCompressionInSingleFile=true
    }
    Write-Host "Published $Rid -> $OutDir" -ForegroundColor Green
}

Write-Host "=== Sumrak VPN all Windows targets ===" -ForegroundColor Cyan

& (Join-Path $PSScriptRoot "sync-android-icon.ps1")

if (-not $SkipXrayDownload) {
    & (Join-Path $PSScriptRoot "download-xray.ps1")
    & (Join-Path $PSScriptRoot "download-singbox.ps1")
    & (Join-Path $PSScriptRoot "download-tun2.ps1")
}

& (Join-Path $PSScriptRoot "create-icon.ps1")

$pubX64 = Join-Path $Root "publish"
$pubX86 = Join-Path $Root "publish-x86"
$pubLite = Join-Path $Root "publish-lite"

Publish-Sumrak -Rid "win-x64" -OutDir $pubX64
Publish-Sumrak -Rid "win-x86" -OutDir $pubX86

if ($Lite) {
    Publish-Sumrak -Rid "win-x64" -OutDir $pubLite -FrameworkDependent
}

foreach ($base in @($pubX64, $pubX86, $pubLite)) {
    if (-not (Test-Path $base)) { continue }
    foreach ($sub in @("xray", "sing-box", "tun2")) {
        $src = Join-Path $Root "runtime\$sub"
        if (-not (Test-Path $src)) { continue }
        $dst = Join-Path $base "bin\$sub"
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item "$src\*" $dst -Recurse -Force
    }
}

if ($BuildInstaller -and (Test-Path "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe")) {
  & (Join-Path $PSScriptRoot "download-dotnet-runtimes.ps1")
  $iscc = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
  & $iscc (Join-Path $Root "installer\SumraXSetup.iss")
  if (Test-Path (Join-Path $Root "installer\SumraXSetup-x86.iss")) {
    & $iscc (Join-Path $Root "installer\SumraXSetup-x86.iss")
  }
  if ($Lite -and (Test-Path (Join-Path $Root "installer\SumraXSetup-Lite.iss"))) {
    & $iscc (Join-Path $Root "installer\SumraXSetup-Lite.iss")
  }
}

$litePath = if ($Lite) { $pubLite } else { "skipped" }
Write-Host "Done. x64: $pubX64 | x86: $pubX86 | lite: $litePath" -ForegroundColor Cyan