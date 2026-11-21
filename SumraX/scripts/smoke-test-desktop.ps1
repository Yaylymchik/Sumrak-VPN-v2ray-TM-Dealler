# Smoke-test all SumraX desktop publish variants on this machine
param([int]$WarmupSeconds = 4)

$ErrorActionPreference = "Continue"
$Root = Split-Path -Parent $PSScriptRoot
$results = New-Object System.Collections.Generic.List[object]

function Get-FileVersionString([string]$path) {
    if (-not (Test-Path $path)) { return $null }
    try { return (Get-Item $path).VersionInfo.FileVersion } catch { return $null }
}

function Test-XrayBundle([string]$dir) {
    $xray = Join-Path $dir "bin\xray\xray.exe"
    $sb = Join-Path $dir "bin\sing-box\sing-box.exe"
    @{
        Xray = (Test-Path $xray)
        SingBox = (Test-Path $sb)
        XrayVersion = if (Test-Path $xray) { Get-FileVersionString $xray } else { $null }
    }
}

function Test-LaunchExe([string]$label, [string]$exePath, [string]$workDir) {
    $row = [ordered]@{
        Variant     = $label
        Path        = $exePath
        Exists      = Test-Path $exePath
        FileVersion = $null
        Launch      = "skip"
        ExitMs      = 0
        Notes       = ""
    }
    if (-not $row.Exists) {
        $row.Notes = "file missing"
        return $row
    }
    $row.FileVersion = Get-FileVersionString $exePath
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $exePath
    $psi.WorkingDirectory = if ($workDir) { $workDir } else { Split-Path $exePath -Parent }
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
  try {
    $p = [System.Diagnostics.Process]::Start($psi)
    if (-not $p) {
      $row.Launch = "fail"
      $row.Notes = "Process.Start returned null"
      return $row
    }
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $exited = $p.WaitForExit($WarmupSeconds * 1000)
    $sw.Stop()
    $row.ExitMs = [int]$sw.ElapsedMilliseconds
    if ($exited) {
      $row.Launch = if ($p.ExitCode -eq 0) { "exit-ok" } else { "exit-$($p.ExitCode)" }
      $row.Notes = "Exited within ${WarmupSeconds}s"
    } else {
      $row.Launch = "running"
      $row.Notes = "Still running after ${WarmupSeconds}s (expected for WPF)"
      try { $p.Kill() } catch {}
    }
  } catch {
    $row.Launch = "fail"
    $row.Notes = $_.Exception.Message
  }
  return $row
}

function Test-LaunchDotNet([string]$label, [string]$dir) {
    $dll = Join-Path $dir "Sumrak VPN.dll"
    $cfg = Join-Path $dir "Sumrak VPN.runtimeconfig.json"
    if (-not (Test-Path $dll)) {
      return [ordered]@{ Variant = $label; Launch = "skip"; Notes = "no dll" }
    }
    $tfm = "unknown"
    if (Test-Path $cfg) {
        $j = Get-Content $cfg -Raw | ConvertFrom-Json
        $tfm = $j.runtimeOptions.frameworks[0].framework
    }
    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
      return [ordered]@{ Variant = $label; Launch = "skip"; Notes = "dotnet CLI missing"; Tfm = $tfm }
    }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $dotnet.Path
    $psi.Arguments = "`"$dll`""
    $psi.WorkingDirectory = $dir
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true
    $row = [ordered]@{ Variant = "$label (dotnet)"; Tfm = $tfm; Launch = "fail"; Notes = "" }
    try {
      $p = [System.Diagnostics.Process]::Start($psi)
      $exited = $p.WaitForExit($WarmupSeconds * 1000)
      if ($exited) {
        $row.Launch = "exit-$($p.ExitCode)"
        $row.Notes = "dll host exited"
      } else {
        $row.Launch = "running"
        $row.Notes = "OK"
        try { $p.Kill() } catch {}
      }
    } catch {
      $row.Notes = $_.Exception.Message
    }
    return $row
}

$variants = @(
    @{ Label = "x64 self-contained"; Dir = Join-Path $Root "publish"; Exe = "Sumrak VPN.exe" },
    @{ Label = "x86 self-contained"; Dir = Join-Path $Root "publish-x86"; Exe = "Sumrak VPN.exe" },
    @{ Label = "Win7 x64 FDD"; Dir = Join-Path $Root "publish-win7"; Exe = "Sumrak VPN.exe" },
    @{ Label = "Win7 x86 FDD"; Dir = Join-Path $Root "publish-win7-x86"; Exe = "Sumrak VPN.exe" },
    @{ Label = "Lite x64 FDD"; Dir = Join-Path $Root "publish-lite"; Exe = "Sumrak VPN.exe" }
)

Write-Host "=== SumraX desktop smoke test ===" -ForegroundColor Cyan
Write-Host "Host: $([Environment]::OSVersion.VersionString) $(if ([Environment]::Is64BitOperatingSystem) {'x64'} else {'x86'})"

foreach ($v in $variants) {
    $exe = Join-Path $v.Dir $v.Exe
    $bundle = Test-XrayBundle $v.Dir
    $launch = Test-LaunchExe $v.Label $exe $v.Dir
    $dllTest = $null
    if ($v.Label -match "FDD") {
        $dllTest = Test-LaunchDotNet $v.Label $v.Dir
    }
    $results.Add([pscustomobject]@{
        Variant   = $v.Label
        Version   = $launch.FileVersion
        Xray      = $bundle.Xray
        SingBox   = $bundle.SingBox
        Launch    = $launch.Launch
        Ms        = $launch.ExitMs
        DotNet    = if ($dllTest) { $dllTest.Launch } else { "-" }
        Notes     = @($launch.Notes, $dllTest.Notes, $(if (-not $bundle.Xray) { "no xray" }) | Where-Object { $_ }) -join "; "
    }) | Out-Null
}

# Installed payload versions inside Smart (Inno stores version in PE)
$installers = @(
    "D:\Sumrak\SumraX-VPN-Setup-2.2.21.exe",
    "D:\Sumrak\SumraX-VPN-Setup-2.2.21-x86.exe",
    "D:\Sumrak\SumraX-VPN-Setup-2.2.21-Win7.exe",
    "D:\Sumrak\SumraX-VPN-Setup-2.2.21-Win7-x86.exe",
    "D:\Sumrak\SumraX-VPN-Setup-2.2.21-Smart.exe"
)
Write-Host "`n=== Installers (PE exists) ===" -ForegroundColor Cyan
foreach ($i in $installers) {
    $ok = Test-Path $i
    $mb = if ($ok) { [math]::Round((Get-Item $i).Length / 1MB, 2) } else { 0 }
    Write-Host ("{0,-45} {1,8} MB  {2}" -f (Split-Path $i -Leaf), $mb, $(if ($ok) { "OK" } else { "MISSING" }))
}

$results | Format-Table -AutoSize

$bad = $results | Where-Object { $_.Launch -eq "fail" -or $_.Launch -like "exit-*" -and $_.Launch -ne "exit-ok" }
if ($bad) {
    Write-Host "WARN: some launches failed or exited early" -ForegroundColor Yellow
    exit 1
}
Write-Host "All runnable variants OK on this host (Win10+ x64)." -ForegroundColor Green
exit 0