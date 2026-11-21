# Download offline .NET Desktop Runtime installers (bundled into SumraX setup)
param(
    [string]$DotNet6Version = "6.0.36",
    [string]$DotNet8Version = "8.0.28"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $Root "runtime\dotnet-redist"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$files = @(
    @{ Name = "windowsdesktop-runtime-$DotNet6Version-win-x64.exe"; Url = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$DotNet6Version/windowsdesktop-runtime-$DotNet6Version-win-x64.exe" },
    @{ Name = "windowsdesktop-runtime-$DotNet6Version-win-x86.exe"; Url = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$DotNet6Version/windowsdesktop-runtime-$DotNet6Version-win-x86.exe" },
    @{ Name = "windowsdesktop-runtime-$DotNet8Version-win-x64.exe"; Url = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$DotNet8Version/windowsdesktop-runtime-$DotNet8Version-win-x64.exe" },
    @{ Name = "windowsdesktop-runtime-$DotNet8Version-win-x86.exe"; Url = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$DotNet8Version/windowsdesktop-runtime-$DotNet8Version-win-x86.exe" }
)

foreach ($f in $files) {
    $dest = Join-Path $OutDir $f.Name
    if ((Test-Path $dest) -and (Get-Item $dest).Length -gt 1MB) {
        Write-Host "Skip (exists): $($f.Name)" -ForegroundColor DarkGray
        continue
    }
    Write-Host "Download: $($f.Name)" -ForegroundColor Cyan
    Invoke-WebRequest -Uri $f.Url -OutFile $dest -UseBasicParsing
}

Write-Host "Dotnet redistributables ready in $OutDir" -ForegroundColor Green