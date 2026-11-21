$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$p = Join-Path $env:APPDATA 'Sumrak VPN\settings.json'
$raw=(Get-Content $p -Raw).Trim()
$ent=[Text.Encoding]::UTF8.GetBytes('SumraX.VPN.v1')
$plain=[Security.Cryptography.ProtectedData]::Unprotect([Convert]::FromBase64String($raw),$ent,[Security.Cryptography.DataProtectionScope]::CurrentUser)
$j=[Text.Encoding]::UTF8.GetString($plain)|ConvertFrom-Json
$u=@($j.SubscriptionUrls)[0]
"raw length      : $($u.Length)"
"scheme          : $(([Uri]$u).Scheme)"
"host            : $(([Uri]$u).Host)"
"absolutePath    : '$(([Uri]$u).AbsolutePath)'"
"query           : len=$(([Uri]$u).Query.Length)"
"fragment        : $(([Uri]$u).Fragment)"
"has whitespace  : $($u -match '\s')"
"starts with     : $($u.Substring(0,[Math]::Min(28,$u.Length)))"
"ends with       : $($u.Substring([Math]::Max(0,$u.Length-14)))"
