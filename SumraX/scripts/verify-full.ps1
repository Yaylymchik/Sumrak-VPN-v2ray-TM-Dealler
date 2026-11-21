# Sumrak VPN — extended full verification
param(
    [string]$Configuration = "Release",
    [switch]$Offline,
    [switch]$SkipSmokeTest,
    [switch]$SkipInstaller
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
$TotalSteps = 8

Write-Host "=== Sumrak VPN EXTENDED VERIFICATION ===" -ForegroundColor Cyan
if ($Offline) { Write-Host "Mode: OFFLINE" -ForegroundColor DarkGray }

Write-Host "[1/$TotalSteps] Restore + Build..." -ForegroundColor Yellow
dotnet restore (Join-Path $Root "SumraX.sln") --disable-parallel 2>&1 | Out-Null
dotnet build (Join-Path $Root "SumraX.sln") -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "  OK" -ForegroundColor Green

Write-Host "[2/$TotalSteps] Icons..." -ForegroundColor Yellow
foreach ($p in @($IconPng, $IconIco)) {
    if (-not (Test-Path $p)) { throw "Missing icon: $p" }
    Write-Host "  $(Split-Path $p -Leaf) ($((Get-Item $p).Length) bytes)" -ForegroundColor Green
}

Write-Host "[3/$TotalSteps] Publish..." -ForegroundColor Yellow
if ($Offline) {
    & (Join-Path $PSScriptRoot "build-offline.ps1") -Configuration $Configuration -SkipInstaller:$SkipInstaller
} else {
    $pubArgs = @{ Configuration = $Configuration }
    & (Join-Path $PSScriptRoot "publish.ps1") @pubArgs
    if (Test-Path (Join-Path $Root "runtime\xray")) {
        Copy-Item "$Root\runtime\xray\*" (Join-Path $PublishDir "bin\xray") -Recurse -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path (Join-Path $Root "runtime\sing-box\sing-box.exe")) {
        $dst = Join-Path $PublishDir "bin\sing-box"
        New-Item -ItemType Directory -Force -Path $dst | Out-Null
        Copy-Item "$Root\runtime\sing-box\*" $dst -Recurse -Force
    }
}
Write-Host "  OK" -ForegroundColor Green

Write-Host "[4/$TotalSteps] Artifacts..." -ForegroundColor Yellow
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

$runtimeConfig = Join-Path $PublishDir "Sumrak VPN.runtimeconfig.json"
if (Test-Path $runtimeConfig) {
    $cfgText = Get-Content $runtimeConfig -Raw
    if ($cfgText -match '"frameworks"\s*:') {
        throw "Sumrak VPN.exe is framework-dependent (needs .NET 8). Re-run publish.ps1 with --self-contained true"
    }
    Write-Host "  self-contained publish OK" -ForegroundColor Green
}

Write-Host "[5/$TotalSteps] Subscription import smoke..." -ForegroundColor Yellow
$subOut = dotnet run --project (Join-Path $Root "tools\SubscriptionSmoke\SubscriptionSmoke.csproj") -c $Configuration 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host $subOut; throw "Subscription smoke failed" }
Write-Host "  $subOut" -ForegroundColor Green

Write-Host "[6/$TotalSteps] Crypt link decrypt smoke..." -ForegroundColor Yellow
$cryptSmokeOut = dotnet run --project (Join-Path $Root "tools\CryptLinkSmoke\CryptLinkSmoke.csproj") -c $Configuration 2>&1
if ($LASTEXITCODE -ne 0) { Write-Host $cryptSmokeOut; throw "Crypt link decrypt test failed" }
Write-Host "  $cryptSmokeOut" -ForegroundColor Green

Write-Host "[7/$TotalSteps] Core binaries smoke..." -ForegroundColor Yellow
& $Xray version 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "xray version failed" }
Write-Host "  xray version OK" -ForegroundColor Green
& $SingBox version 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw "sing-box version failed" }
Write-Host "  sing-box version OK" -ForegroundColor Green

if (-not $SkipSmokeTest) {
    Write-Host "[8/$TotalSteps] Launch smoke test (12s)..." -ForegroundColor Yellow
    Get-Process "Sumrak VPN" -ErrorAction SilentlyContinue | Stop-Process -Force
    $proc = Start-Process -FilePath $Exe -PassThru
    Start-Sleep -Seconds 12
    if (-not (Get-Process -Id $proc.Id -ErrorAction SilentlyContinue)) {
        throw "Process exited within 12s (startup crash?)"
    }
    Write-Host "  Process running (PID $($proc.Id))" -ForegroundColor Green
    Stop-Process -Id $proc.Id -Force
} else {
    Write-Host "[8/$TotalSteps] Launch smoke test skipped" -ForegroundColor DarkGray
}

if (Test-Path $DistDir) {
    $setup = Get-ChildItem $DistDir -Filter "SumraX-VPN-Setup-*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($setup) {
        $mb = [math]::Round($setup.Length / 1MB, 1)
        Write-Host "Installer: $($setup.Name) ($mb MB)" -ForegroundColor Green
    }
}

$pubSize = [math]::Round((Get-ChildItem $PublishDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host ""
Write-Host "ALL CHECKS PASSED ($pubSize MB publish)" -ForegroundColor Green
Write-Host "Run: $Exe"
