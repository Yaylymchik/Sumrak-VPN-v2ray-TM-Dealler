param(
    [string]$BaseKey = $env:SUMRAX_REF_ORG_REGISTRY
)

if (-not $BaseKey) {
    Write-Host "Set SUMRAX_REF_ORG_REGISTRY to HKCU:\Software\<Vendor>\<App>\OrganizationDefaults\Preferences"
    exit 0
}

foreach ($k in @('Subscriptions','Updates')) {
    $p = Join-Path $BaseKey $k
    if (Test-Path $p) {
        "=== $k ==="
        (Get-ItemProperty $p).PSObject.Properties | Where-Object { $_.Name -notlike 'PS*' } | ForEach-Object {
            "{0} = {1}" -f $_.Name, $_.Value
        }
    }
}