$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
[Console]::OutputEncoding = [Text.Encoding]::UTF8
$rows = Get-Content (Join-Path $PSScriptRoot '_dump.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$entropy = [Text.Encoding]::UTF8.GetBytes('SumraX.VPN.v1')

$dec = New-Object System.Collections.ArrayList
foreach ($row in $rows) {
    $bytes = [Convert]::FromBase64String($row.payload)
    $plain = [Security.Cryptography.ProtectedData]::Unprotect($bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    [void]$dec.Add(([Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json))
}

"payload keys: " + (($dec[0].PSObject.Properties.Name) -join ', ')
""
$ep = $dec | ForEach-Object { "$($_.Protocol)|$($_.Address)|$($_.Port)|$($_.UserId)" }
"profiles        : $($dec.Count)"
"unique endpoints: $(($ep | Sort-Object -Unique).Count)"
""
"top duplicated endpoints:"
$ep | Group-Object | Sort-Object Count -Descending | Select-Object -First 5 | ForEach-Object { "  $($_.Count) x  $($_.Name)" }
""
"protocols in db:"
$dec | Group-Object Protocol | Sort-Object Count -Descending | ForEach-Object { "  $($_.Name) $($_.Count)" }
