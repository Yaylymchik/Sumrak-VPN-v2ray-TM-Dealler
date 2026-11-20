# Re-download Gradle + AGP from the internet, then fast APK build (daemon, no --refresh-dependencies)
param(
    [switch]$SkipReinstall
)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$V2 = Join-Path $Root "V2rayNG"
Set-Location $V2

function Download-Curl($Url, $Dest) {
    $dir = Split-Path $Dest -Parent
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
    Write-Host "GET $Url"
    & curl.exe -fL --connect-timeout 30 --max-time 600 -o $Dest $Url
    if (-not (Test-Path $Dest)) { throw "Download failed: $Url" }
    $len = (Get-Item $Dest).Length
    if ($len -lt 1024) { throw "File too small: $Dest ($len bytes)" }
}

if (-not $SkipReinstall) {
    Write-Host "[1/4] Gradle Wrapper 9.5.1..."
    $wrapperRoot = Join-Path $env:USERPROFILE ".gradle\wrapper\dists\gradle-9.5.1-bin"
    $installed = Get-ChildItem $wrapperRoot -Recurse -Filter "gradle.bat" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($installed) {
        Write-Host "Gradle already unpacked: $($installed.DirectoryName)"
    } else {
        $sub = Get-ChildItem $wrapperRoot -Directory -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $sub) {
            New-Item -ItemType Directory -Force -Path (Join-Path $wrapperRoot "iq79hdu3mqx29lgffhp8bfmx") | Out-Null
            $sub = Get-ChildItem $wrapperRoot -Directory | Select-Object -First 1
        }
        Get-ChildItem $sub.FullName -Filter "*.zip*" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
        try {
            Download-Curl "https://services.gradle.org/distributions/gradle-9.5.1-bin.zip" (Join-Path $sub.FullName "gradle-9.5.1-bin.zip")
        } catch {
            Write-Host "Gradle zip download skipped (DNS): use existing wrapper."
        }
    }

    Write-Host "[2/4] Android Gradle Plugin 9.2.1 jar..."
    $agpTmp = Join-Path $Root ".build-cache\gradle-9.2.1.jar"
    Download-Curl "https://dl.google.com/dl/android/maven2/com/android/tools/build/gradle/9.2.1/gradle-9.2.1.jar" $agpTmp
    Remove-Item "$env:USERPROFILE\.gradle\.tmp\gradle_download*" -Force -ErrorAction SilentlyContinue

    Write-Host "[3/4] Warm dependencies (one pass)..."
    & .\gradlew.bat :app:prePlaystoreDebugBuild -PABI_FILTERS=arm64-v8a --console=plain 2>&1 | Out-Null
}

Write-Host "[4/4] assemblePlaystoreDebug (daemon, cached deps)..."
& .\gradlew.bat assemblePlaystoreDebug -PABI_FILTERS=arm64-v8a --console=plain
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$apk = Get-ChildItem -Path "app\build\outputs\apk" -Recurse -Filter "*arm64-v8a*.apk" -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if ($apk) { Write-Host "APK: $($apk.FullName)" }
exit 0