# Decrypt dumped payloads (DPAPI, CurrentUser) and aggregate quietly.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$OutputEncoding = [Text.Encoding]::UTF8
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$dump = Join-Path $PSScriptRoot '_dump.json'
$rows = Get-Content $dump -Raw -Encoding UTF8 | ConvertFrom-Json

$entropy = [Text.Encoding]::UTF8.GetBytes('SumraX.VPN.v1')
$bySub = @{}
$ok = 0; $fail = 0
$names = New-Object System.Collections.ArrayList
$subsRaw = @{}

foreach ($row in $rows) {
    try {
        $bytes = [Convert]::FromBase64String($row.payload)
        $plain = [Security.Cryptography.ProtectedData]::Unprotect($bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $o = [Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json
        $ok++
        $sub = $o.SubscriptionUrl
        if ([string]::IsNullOrWhiteSpace($sub)) { $key = '<no subscription url>' }
        else {
            try { $u = [Uri]$sub; $key = "$($u.Scheme)://$($u.Host) (len=$($sub.Length))"; $subsRaw[$key] = $sub } catch { $key = '<unparsable>' }
        }
        if (-not $bySub.ContainsKey($key)) { $bySub[$key] = 0 }
        $bySub[$key]++
        [void]$names.Add([string]$o.Name)
    } catch { $fail++ }
}

"decrypted   : $ok"
"failed      : $fail"
""
"--- profiles per subscription ---"
$bySub.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { "  $($_.Value) x  $($_.Key)" }
""
"--- last 10 names ---"
$names | Select-Object -Last 10 | ForEach-Object { '  ' + (($_ -replace '\s+',' ')) }
