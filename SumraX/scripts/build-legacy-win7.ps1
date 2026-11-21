# Windows 7 SP1 / 8.1 — net6.0-windows7.0 framework-dependent publish
param(
    [string]$Configuration = "Release",
    [switch]$SkipXrayDownload,
    [switch]$BuildInstaller
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$LegacyTfm = "net6.0-windows7.0"
$AppProj = Join-Path $Root "src\SumraX.App\SumraX.App.csproj"

Write-Host "=== Sumrak VPN legacy Win7/8.1 ($LegacyTfm) ===" -ForegroundColor Cyan

if (-not $SkipXrayDownload) {
    & (Join-Path $PSScriptRoot "download-xray.ps1")
    & (Join-Path $PSScriptRoot "download-singbox.ps1")
    & (Join-Path $PSScriptRoot "download-tun2.ps1")
}

& (Join-Path $PSScriptRoot "create-icon.ps1")

$pubX64 = Join-Path $Root "publish-win7"
$pubX86 = Join-Path $Root "publish-win7-x86"

foreach ($pair in @(
        @{ Rid = "win-x64"; Out = $pubX64 },
        @{ Rid = "win-x86"; Out = $pubX86 }
    )) {
    dotnet publish $AppProj -c $Configuration -f $LegacyTfm -r $pair.Rid -o $pair.Out `
        --self-contained false `
        -p:PublishSingleFile=false
    Write-Host "Published $($pair.Rid) -> $($pair.Out)" -ForegroundColor Green
}

foreach ($base in @($pubX64, $pubX86)) {
    foreach ($sub in @("xray", "sing-box", "tun2")) {
        $src = Join-Path $Root "runtime\$sub"
        if (-not (Test-Path $src)) { continue }
        $dst = Join-Path $base "bin\$sub"
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item "$src\*" $dst -Recurse -Force
    }
}

$readme = @"
Sumrak VPN 2.2.21 — сборка для Windows 7 SP1 / 8.1
==================================================
Требуется установленный .NET 6 Desktop Runtime (x64 или x86):
https://dotnet.microsoft.com/download/dotnet/6.0

Рекомендуется режим «Системный прокси» вместо TUN на старых ОС.
Запуск: Sumrak VPN.exe
"@
Set-Content -Path (Join-Path $pubX64 "README-Win7.txt") -Value $readme -Encoding UTF8
Copy-Item (Join-Path $pubX64 "README-Win7.txt") (Join-Path $pubX86 "README-Win7.txt") -Force

if ($BuildInstaller) {
    & (Join-Path $PSScriptRoot "download-dotnet-runtimes.ps1")
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($iscc) {
        & $iscc (Join-Path $Root "installer\SumraXSetup-Win7.iss")
        & $iscc (Join-Path $Root "installer\SumraXSetup-Win7-x86.iss")
    }
    else {
        Write-Warning 'Inno Setup 6 not found; skipped installer.'
    }
}

Write-Host "Done. Win7 x64: $pubX64 ; x86: $pubX86" -ForegroundColor Cyan