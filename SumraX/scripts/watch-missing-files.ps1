# Every 40 minutes: check missing offline/runtime files and try to download them.
param(
    [int]$IntervalMinutes = 40
)

$Script = Join-Path $PSScriptRoot "check-missing-files.ps1"
$Seconds = $IntervalMinutes * 60

Write-Host "SumraX missing-files watcher: every ${IntervalMinutes}m" -ForegroundColor Cyan
Write-Host "Log: SumraX\logs\missing-files-check.log" -ForegroundColor DarkGray
Write-Host "Stop: Ctrl+C in this terminal" -ForegroundColor DarkGray

while ($true) {
    & $Script -AutoDownload
    Start-Sleep -Seconds $Seconds
    Write-Output "AGENT_LOOP_TICK_missing_files {""prompt"":""Run check-missing-files.ps1 -AutoDownload and report missing files for Mac offline bundle""}"
}
