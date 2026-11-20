# Fix DNS-related Gradle download failures; prefetch key jars; single assemble.
$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $PSScriptRoot
$V2 = Join-Path $Root "V2rayNG"
$Cache = Join-Path $Root ".build-cache"
New-Item -ItemType Directory -Force -Path $Cache | Out-Null

function Get-Curl($Url, $Out) {
    Write-Host "  curl $Url"
    & curl.exe -fL --connect-timeout 20 --retry 5 --retry-delay 3 --max-time 900 -o $Out $Url
    return $LASTEXITCODE -eq 0 -and (Test-Path $Out) -and ((Get-Item $Out).Length -gt 500)
}

Set-Location $V2
& .\gradlew.bat --stop 2>$null
Remove-Item "$env:USERPROFILE\.gradle\.tmp\gradle_download*" -Force -ErrorAction SilentlyContinue

Write-Host "[1] Gradle wrapper"
$gradleBat = Get-ChildItem "$env:USERPROFILE\.gradle\wrapper\dists\gradle-9.5.1-bin" -Recurse -Filter gradle.bat -ErrorAction SilentlyContinue | Select-Object -First 1
if ($gradleBat) {
    Write-Host "  OK $($gradleBat.FullName)"
} else {
    Write-Warning "Gradle 9.5.1 not unpacked"
}

Write-Host "[2] Prefetch via curl"
$urls = @(
    @{ U = "https://dl.google.com/dl/android/maven2/com/android/tools/build/gradle/9.2.1/gradle-9.2.1.jar"; F = "gradle-9.2.1.jar" },
    @{ U = "https://repo1.maven.org/maven2/com/tencent/mmkv-static/1.3.16/mmkv-static-1.3.16.aar"; F = "mmkv-static-1.3.16.aar" },
    @{ U = "https://repo1.maven.org/maven2/com/blacksquircle/ui/editorkit/2.9.0/editorkit-2.9.0.aar"; F = "editorkit-2.9.0.aar" },
    @{ U = "https://maven.aliyun.com/repository/google/androidx/sqlite/sqlite-android/2.5.0/sqlite-android-2.5.0.aar"; F = "sqlite-android-2.5.0.aar" }
)
foreach ($item in $urls) {
    $out = Join-Path $Cache $item.F
    if ((Test-Path $out) -and (Get-Item $out).Length -gt 500) { continue }
    if (-not (Get-Curl $item.U $out)) {
        Write-Warning "Skip: $($item.F)"
    }
}

Write-Host "[3] Resolve dependencies"
& .\gradlew.bat :app:prePlaystoreDebugBuild -PABI_FILTERS=arm64-v8a --console=plain 2>&1 | Out-Null

Write-Host "[4] assemblePlaystoreDebug"
& .\gradlew.bat assemblePlaystoreDebug -PABI_FILTERS=arm64-v8a --console=plain
exit $LASTEXITCODE