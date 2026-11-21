# Read-only inspection of profile payloads (no credentials printed).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security

$db = Join-Path $env:APPDATA 'Sumrak VPN\sumrax.db'
$tmp = Join-Path $env:TEMP ('sumrax_copy_' + [guid]::NewGuid().ToString('N') + '.db')
Copy-Item $db $tmp -Force
$wal = "$db-wal"; if (Test-Path $wal) { Copy-Item $wal "$tmp-wal" -Force }

$asm = [System.Reflection.Assembly]::LoadWithPartialName('System.Data.SQLite')
if (-not $asm) { 'NO System.Data.SQLite provider'; exit 2 }

$conn = New-Object System.Data.SQLite.SQLiteConnection("Data Source=$tmp;Read Only=True")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = 'select id, name, payload from servers'
$r = $cmd.ExecuteReader()

$entropy = [Text.Encoding]::UTF8.GetBytes('SumraX.VPN.v1')
$bySub = @{}
$junk = 0
$ok = 0
$fail = 0
$names = New-Object System.Collections.ArrayList
$total = 0

while ($r.Read()) {
    $total++
    $name = $r.GetString(1)
    $payload = $r.GetString(2)
    try {
        $bytes = [Convert]::FromBase64String($payload)
        $plain = [Security.Cryptography.ProtectedData]::Unprotect($bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
        $txt = [Text.Encoding]::UTF8.GetString($plain)
        $o = $txt | ConvertFrom-Json
        $ok++
        $sub = $o.SubscriptionUrl
        if ([string]::IsNullOrWhiteSpace($sub)) { $sub = '<none>' } else {
            try { $u = [Uri]$sub; $sub = "$($u.Scheme)://$($u.Host)/...(len=$($sub.Length))" } catch { $sub = '<unparsable>' }
        }
        if (-not $bySub.ContainsKey($sub)) { $bySub[$sub] = 0 }
        $bySub[$sub]++
        $n = if ($o.Name) { $o.Name } else { $name }
        [void]$names.Add($n)
    } catch {
        $fail++
        if ($name -like '#*') { $junk++ }
    }
}
$r.Close(); $conn.Close(); Remove-Item $tmp -Force -ErrorAction SilentlyContinue

"rows total        : $total"
"payload decrypted : $ok"
"payload failed    : $fail"
"names starting '#' : $junk"
""
"--- profiles per subscription ---"
$bySub.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { "  $($_.Key)  => $($_.Value)" }
""
"--- sample profile names (first 8, newest ids last) ---"
$names | Select-Object -Last 8 | ForEach-Object { '  ' + ($_ -replace '\s+', ' ').Substring(0, [Math]::Min(55, ($_ -replace '\s+',' ').Length)) }
