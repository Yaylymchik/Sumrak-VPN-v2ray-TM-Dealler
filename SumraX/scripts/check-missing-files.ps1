# Checks bundled/offline files; optionally downloads missing runtimes.
param(
    [switch]$AutoDownload,
    [switch]$Quiet
)

$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $PSScriptRoot
$LogDir = Join-Path $Root "logs"
$LogFile = Join-Path $LogDir "missing-files-check.log"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

function Log([string]$msg, [string]$color = "White") {
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $msg
    Add-Content -Path $LogFile -Value $line -Encoding UTF8
    if (-not $Quiet) { Write-Host $line -ForegroundColor $color }
}

$required = @(
    @{ Group = "Windows/xray"; Path = "runtime\xray\xray.exe"; MinBytes = 10MB },
    @{ Group = "Windows/xray"; Path = "runtime\xray\geoip.dat"; MinBytes = 1MB },
    @{ Group = "Windows/xray"; Path = "runtime\xray\geosite.dat"; MinBytes = 1MB },
    @{ Group = "Windows/sing-box"; Path = "runtime\sing-box\sing-box.exe"; MinBytes = 5MB },
    @{ Group = "Mac-arm64/xray"; Path = "runtime\mac-arm64\xray\xray"; MinBytes = 10MB },
    @{ Group = "Mac-arm64/xray"; Path = "runtime\mac-arm64\xray\geoip.dat"; MinBytes = 1MB },
    @{ Group = "Mac-arm64/xray"; Path = "runtime\mac-arm64\xray\geosite.dat"; MinBytes = 1MB },
    @{ Group = "Mac-arm64/sing-box"; Path = "runtime\mac-arm64\sing-box\sing-box"; MinBytes = 5MB },
    @{ Group = "Mac-x64/xray"; Path = "runtime\mac-x64\xray\xray"; MinBytes = 10MB },
    @{ Group = "Mac-x64/xray"; Path = "runtime\mac-x64\xray\geoip.dat"; MinBytes = 1MB },
    @{ Group = "Mac-x64/xray"; Path = "runtime\mac-x64\xray\geosite.dat"; MinBytes = 1MB },
    @{ Group = "Mac-x64/sing-box"; Path = "runtime\mac-x64\sing-box\sing-box"; MinBytes = 5MB },
    @{ Group = "Desktop/icons"; Path = "src\SumraX.App.Desktop\Assets\sumrax.ico"; MinBytes = 1KB },
    @{ Group = "Scripts"; Path = "scripts\build-mac-offline.sh"; MinBytes = 100 },
    @{ Group = "Scripts"; Path = "scripts\package-osx.sh"; MinBytes = 100 }
)

$optional = @(
    @{ Group = "Publish"; Path = "publish\Sumrak VPN.exe"; MinBytes = 30MB },
    @{ Group = "Publish"; Path = "publish\bin\xray\xray.exe"; MinBytes = 10MB },
    @{ Group = "Mac-build"; Path = "publish-mac\Sumrak VPN.app"; MinBytes = 0; IsDir = $true }
)

$missing = [System.Collections.Generic.List[string]]@()
$ok = 0

Log "=== Missing files check ===" "Cyan"

foreach ($item in $required) {
    $full = Join-Path $Root $item.Path
    $exists = Test-Path $full
    $sizeOk = $false
    if ($exists -and -not $item.IsDir) {
        $sizeOk = (Get-Item $full).Length -ge $item.MinBytes
    }
    elseif ($exists -and $item.IsDir) {
        $sizeOk = $true
    }

    if ($exists -and $sizeOk) {
        $ok++
    }
    else {
        $reason = if (-not $exists) { "MISSING" } else { "TOO_SMALL" }
        $missing.Add("$reason`t$($item.Group)`t$($item.Path)")
        Log "  [$reason] $($item.Path)" "Red"
    }
}

foreach ($item in $optional) {
    $full = Join-Path $Root $item.Path
    if (-not (Test-Path $full)) {
        Log "  [OPTIONAL MISSING] $($item.Path)" "DarkYellow"
    }
}

# NuGet Desktop restore probe
$desktopProj = Join-Path $Root "src\SumraX.App.Desktop\SumraX.App.Desktop.csproj"
if (Test-Path $desktopProj) {
    $restoreOut = dotnet restore $desktopProj 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0) {
        $missing.Add("NUGET_FAIL`tDesktop/Avalonia`trestore failed")
        Log "  [NUGET_FAIL] SumraX.App.Desktop restore" "Red"
        if (-not $Quiet) { $restoreOut -split "`n" | Select-Object -Last 5 | ForEach-Object { Log "    $_" "DarkGray" } }
    }
    else {
        Log "  [OK] SumraX.App.Desktop NuGet restore" "Green"
    }
}

if ($AutoDownload -and $missing.Count -gt 0) {
    Log "Auto-download attempt..." "Yellow"
    $needWinXray = $missing | Where-Object { $_ -match "Windows/xray" }
    $needSing = $missing | Where-Object { $_ -match "sing-box" }
    $needMac = $missing | Where-Object { $_ -match "Mac-" }

    if ($needWinXray) {
        try { & (Join-Path $PSScriptRoot "download-xray.ps1") } catch { Log "  download-xray.ps1 failed: $_" "Red" }
    }
    if ($needSing -and ($needSing -match "Windows")) {
        try { & (Join-Path $PSScriptRoot "download-singbox.ps1") } catch { Log "  download-singbox.ps1 failed: $_" "Red" }
    }
    if ($needMac) {
        try { & (Join-Path $PSScriptRoot "download-mac-runtimes.ps1") -Arch both } catch { Log "  download-mac-runtimes.ps1 failed: $_" "Red" }
    }
}

$summary = "Required OK: $ok/$($required.Count) | Missing: $($missing.Count)"
Log $summary $(if ($missing.Count -eq 0) { "Green" } else { "Yellow" })

if ($missing.Count -gt 0) {
    $reportPath = Join-Path $LogDir "missing-files-latest.txt"
    $missing | Set-Content -Path $reportPath -Encoding UTF8
    Log "Report: $reportPath" "DarkGray"
    exit 1
}

exit 0
