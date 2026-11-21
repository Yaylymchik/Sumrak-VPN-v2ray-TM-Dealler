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

$dec | Group-Object { ([datetime]$_.CreatedAt).ToString('yyyy-MM-dd HH') } | Sort-Object Name | ForEach-Object {
    "  $($_.Name)  -> $($_.Count)"
}
""
"oldest CreatedAt: " + (($dec | Sort-Object CreatedAt | Select-Object -First 1).CreatedAt)
"newest CreatedAt: " + (($dec | Sort-Object CreatedAt | Select-Object -Last 1).CreatedAt)
"now            : " + (Get-Date).ToUniversalTime().ToString('o')
