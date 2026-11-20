# Сборка APK с повтором при ошибках скачивания
param(
    [int]$MaxAttempts = 5
)
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $PSScriptRoot
$V2 = Join-Path $Root "V2rayNG"
$GradleTmp = Join-Path $env:USERPROFILE ".gradle\.tmp"

function Clear-PartialDownloads {
    if (Test-Path $GradleTmp) {
        Get-ChildItem $GradleTmp -Filter "gradle_download*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

Set-Location $V2
$attempt = 0
$log = Join-Path $Root "gradle-build-retry.log"

while ($attempt -lt $MaxAttempts) {
    $attempt++
    Write-Host "=== Attempt $attempt / $MaxAttempts ==="
    Clear-PartialDownloads

    $args = @(
        "assemblePlaystoreDebug",
        "-PABI_FILTERS=arm64-v8a",
        "--console=plain"
    )
    if ($attempt -eq 1) {
        $args += "--refresh-dependencies"
    }

    & .\gradlew.bat @args 2>&1 | Tee-Object -FilePath $log
    $code = $LASTEXITCODE
    if ($code -eq 0) {
        Write-Host "BUILD SUCCESS"
        $apk = Get-ChildItem -Path "app\build\outputs\apk" -Recurse -Filter "*arm64*.apk" -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($apk) { Write-Host "APK: $($apk.FullName)" }
        exit 0
    }

    $tail = Get-Content $log -Tail 40 -ErrorAction SilentlyContinue | Out-String
    $retryable = $tail -match "Could not download|Read timed out|Connection refused|NoHttpResponse|Could not GET|Could not HEAD"
    if (-not $retryable) {
        Write-Host "Non-download failure; stopping."
        exit $code
    }
    Write-Host "Download error - retry in 10s..."
    Start-Sleep -Seconds 10
}

Write-Host "All attempts failed."
exit 1