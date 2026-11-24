# Remove redundant VPN installers from D:\Sumrak (keep current Smart + optional Lite).
param(
    [string]$Version = "2.2.21",
    [string]$OutRoot = "D:\Sumrak"
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path $OutRoot)) { return }

$keep = @(
    "SumraX-VPN-Setup-$Version-Smart.exe",
    "SumraX-VPN-Setup-$Version-Lite.exe"
)

Get-ChildItem -LiteralPath $OutRoot -File -ErrorAction SilentlyContinue |
    Where-Object {
        $n = $_.Name
        ($n -like "SumraX-VPN-Setup-*") -and ($keep -notcontains $n)
    } |
    ForEach-Object {
        Remove-Item $_.FullName -Force
        Write-Host "Removed: $($_.Name)" -ForegroundColor DarkYellow
    }

$dropPortable = @(
    "SumrakVPN_Desktop_${Version}_x64_Portable.exe",
    "SumrakVPN_Desktop_${Version}_x86_Portable.exe",
    "SumrakVPN_Desktop_${Version}_Win7_x64.zip"
)
foreach ($name in $dropPortable) {
    $p = Join-Path $OutRoot $name
    if (Test-Path $p) {
        Remove-Item $p -Force
        Write-Host "Removed: $name" -ForegroundColor DarkYellow
    }
}

Write-Host "Kept in ${OutRoot}: $($keep -join ', ')" -ForegroundColor Green