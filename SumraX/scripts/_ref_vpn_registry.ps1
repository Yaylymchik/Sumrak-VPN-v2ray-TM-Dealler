param(
    [string]$BaseKey = $env:SUMRAX_REF_REGISTRY_BASE
)

if (-not $BaseKey) {
    Write-Host "Set SUMRAX_REF_REGISTRY_BASE to HKCU:\Software\<Vendor>\<App>\Preferences (optional)."
    exit 0
}

if (-not (Test-Path $BaseKey)) {
    Get-ChildItem (Split-Path $BaseKey -Parent) -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Name
    exit 1
}

foreach ($k in 'Subscriptions','Updates','AdvancedSettings') {
    $p = Join-Path $BaseKey $k
    if (Test-Path $p) {
        "=== $k ==="
        $it = Get-ItemProperty $p
        $it.PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } | ForEach-Object { "{0} = {1}" -f $_.Name, $_.Value }
    }
}