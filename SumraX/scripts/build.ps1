# Sumrak VPN Build Script
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$AppProj = Join-Path $Root "src\SumraX.App\SumraX.App.csproj"

Write-Host "Building Sumrak VPN ($Configuration)..." -ForegroundColor Cyan
dotnet restore $Root\SumraX.sln
dotnet build $Root\SumraX.sln -c $Configuration --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

Write-Host "Build succeeded." -ForegroundColor Green
Write-Host "Output: src\SumraX.App\bin\$Configuration\net8.0-windows10.0.19041.0\"
