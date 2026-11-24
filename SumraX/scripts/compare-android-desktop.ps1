# Quick parity report (Android Sumrak VPN vs SumraX PC) — run before release copy to D:\Sumrak
$ErrorActionPreference = "Continue"
$AndroidRoot = "D:\Sumrak\v2rayNG-master\v2rayNG-master"
$DesktopRoot = "D:\Sumrak\v2rayNG-master\v2rayNG-master\SumraX"
$OutRoot = "D:\Sumrak"

function Gradle-Value($name) {
    $gradle = Join-Path $AndroidRoot "V2rayNG\app\build.gradle.kts"
    if (-not (Test-Path $gradle)) { return $null }
    $m = [regex]::Match((Get-Content $gradle -Raw), "$name\s*=\s*""([^""]+)""")
    if ($m.Success) { return $m.Groups[1].Value }
    return $null
}

$issues = New-Object System.Collections.Generic.List[string]

$av = Gradle-Value "versionName"
$avc = Gradle-Value "versionCode"
$iss = Join-Path $DesktopRoot "installer\SumraXSetup.iss"
$desktopVer = $null
if (Test-Path $iss) {
    $dm = [regex]::Match((Get-Content $iss -Raw), '#define MyAppVersion "([^"]+)"')
    if ($dm.Success) { $desktopVer = $dm.Groups[1].Value }
}
if ($av -and $desktopVer -and $av -ne $desktopVer) {
    $issues.Add("Version: Android $av vs desktop installer $desktopVer")
}

$androidIcon = Join-Path $AndroidRoot "V2rayNG\app\src\main\res\mipmap-xxxhdpi\ic_launcher.png"
$desktopIcon = Join-Path $DesktopRoot "src\SumraX.App\Assets\sumrax-icon.png"
if ((Test-Path $androidIcon) -and (Test-Path $desktopIcon)) {
    $ai = Get-Item $androidIcon
    $di = Get-Item $desktopIcon
    if ($di.LastWriteTime -lt $ai.LastWriteTime) {
        $issues.Add("Icon: desktop PNG older than Android ($($di.LastWriteTime) < $($ai.LastWriteTime))")
    }
}

$expected = @(
    "SumraX-VPN-Setup-2.2.21-Smart.exe",
    "SumraX-VPN-Setup-2.2.21-Lite.exe"
)
foreach ($name in $expected) {
    $p = Join-Path $OutRoot $name
    if (-not (Test-Path $p)) { $issues.Add("Missing in D:\Sumrak: $name") }
}

Write-Host "=== Android <-> Desktop parity ===" -ForegroundColor Cyan
Write-Host "Android versionName: $av  versionCode: $avc"
Write-Host "Desktop installer version: $desktopVer"
Write-Host ""

if ($issues.Count -eq 0) {
    Write-Host "OK: versions, icon freshness, D:\Sumrak installers" -ForegroundColor Green
    exit 0
}
Write-Host "Mismatches ($($issues.Count)):" -ForegroundColor Yellow
$issues | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
exit 1