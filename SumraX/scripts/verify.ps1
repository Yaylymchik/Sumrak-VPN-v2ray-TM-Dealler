# Sumrak VPN — full pre-release verification
param(
    [string]$Configuration = "Release",
    [switch]$Offline,
    [switch]$SkipSmokeTest
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$PublishDir = Join-Path $Root "publish"
$Exe = Join-Path $PublishDir "Sumrak VPN.exe"
$Xray = Join-Path $PublishDir "bin\xray\xray.exe"
$SingBox = Join-Path $PublishDir "bin\sing-box\sing-box.exe"
$VersionFile = Join-Path $PublishDir "version.json"
$IconPng = Join-Path $Root "src\SumraX.App\Assets\sumrax-icon.png"
$IconIco = Join-Path $Root "src\SumraX.App\Assets\sumrax.ico"
$DistDir = Join-Path $Root "dist"

Write-Host "=== Sumrak VPN Verification ===" -ForegroundColor Cyan
if ($Offline) { Write-Host "Mode: OFFLINE" -ForegroundColor DarkGray }

Write-Host "[1/6] Restore + Build..." -ForegroundColor Yellow
dotnet restore (Join-Path $Root "SumraX.sln") --disable-parallel 2>&1 | Out-Null
dotnet build (Join-Path $Root "SumraX.sln") -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "  OK" -ForegroundColor Green

Write-Host "[2/6] Icons..." -ForegroundColor Yellow
foreach ($p in @($IconPng, $IconIco)) {
    if (-not (Test-Path $p)) { throw "Missing icon: $p" }
    Write-Host "  $(Split-Path $p -Leaf) ($((Get-Item $p).Length) bytes)" -ForegroundColor Green
}

Write-Host "[3/6] Publish..." -ForegroundColor Yellow
$pubArgs = @{ Configuration = $Configuration }
if ($Offline) { $pubArgs.SkipXrayDownload = $true }
& (Join-Path $PSScriptRoot "publish.ps1") @pubArgs
Write-Host "  OK" -ForegroundColor Green

Write-Host "[4/6] Artifacts..." -ForegroundColor Yellow
$checks = @(
    @{ Path = $Exe; Name = "Sumrak VPN.exe"; MinBytes = 30MB },
    @{ Path = $Xray; Name = "bin\xray\xray.exe"; MinBytes = 10MB },
    @{ Path = $SingBox; Name = "bin\sing-box\sing-box.exe"; MinBytes = 5MB },
    @{ Path = $VersionFile; Name = "version.json"; MinBytes = 10 }
)
foreach ($c in $checks) {
    if (-not (Test-Path $c.Path)) { throw "Missing: $($c.Name)" }
    $size = (Get-Item $c.Path).Length
    if ($size -lt $c.MinBytes) { throw "$($c.Name) too small ($size bytes)" }
    Write-Host "  $($c.Name) ($size bytes)" -ForegroundColor Green
}

Write-Host "[5/6] Crypt link decrypt smoke..." -ForegroundColor Yellow
$cryptSmokeProj = Join-Path $Root "tools\CryptLinkSmoke\CryptLinkSmoke.csproj"
$cryptSmokeOut = dotnet run --project $cryptSmokeProj -c $Configuration 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host $cryptSmokeOut
    throw "Crypt link decrypt test failed"
}
Write-Host "  $cryptSmokeOut" -ForegroundColor Green

if (-not $SkipSmokeTest) {
    Write-Host "[6/6] Launch smoke test..." -ForegroundColor Yellow
    Get-Process "Sumrak VPN" -ErrorAction SilentlyContinue | Stop-Process -Force
    $proc = Start-Process -FilePath $Exe -PassThru
    Start-Sleep -Seconds 12
    if (-not (Get-Process -Id $proc.Id -ErrorAction SilentlyContinue)) {
        throw "Process exited within 12s (startup crash?)"
    }
    Write-Host "  Process running (PID $($proc.Id))" -ForegroundColor Green
    Stop-Process -Id $proc.Id -Force
}
else {
    Write-Host "[6/6] Launch smoke test skipped" -ForegroundColor DarkGray
}

if (Test-Path $DistDir) {
    $setup = Get-ChildItem $DistDir -Filter "SumraX-VPN-Setup-*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($setup) {
        Write-Host "Installer found: $($setup.Name) ($($setup.Length) bytes)" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "ALL CHECKS PASSED" -ForegroundColor Green
Write-Host "Run: $Exe"
