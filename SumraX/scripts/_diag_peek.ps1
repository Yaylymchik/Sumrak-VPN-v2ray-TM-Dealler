# Structured peek at subscription body without exposing server data/tokens.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$path = Join-Path $env:APPDATA 'Sumrak VPN\settings.json'
$raw = (Get-Content $path -Raw).Trim()
$entropy = [Text.Encoding]::UTF8.GetBytes('SumraX.VPN.v1')
$bytes = [Convert]::FromBase64String($raw)
$plain = [Security.Cryptography.ProtectedData]::Unprotect($bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
$j = [Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json
$url = @($j.SubscriptionUrls)[0]

$r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -Headers @{ 'User-Agent' = 'v2rayN/7.0' }
$body = $r.Content
if ($body -is [byte[]]) { $body = [Text.Encoding]::UTF8.GetString($body) }

"length        : $($body.Length)"
"lines         : $((($body -split "`n").Count))"
"first char    : [$([int][char]$body[0])]"
"BOM           : $($body.StartsWith([char]0xFEFF))"

$mask = [regex]::Replace($body.Substring(0, [Math]::Min(600, $body.Length)), '[A-Za-z0-9+/=_\-\\.]{14,}', 'X*')
"--- masked head ---"
$mask
"--- end head ---"

"link schemes : $( ([regex]::Matches($body,'(?im)^(vless|vmess|trojan|ss|ssr|hysteria2?|hy2|tuic|wg)://')).Count )"
"has 'proxies:' : $($body -match 'proxies\s*:')"
"has 'outbounds': $(($body -match 'outbounds').ToString())"
"has 'add: '    : $( ([regex]::Matches($body,'(?m)^\s*-\s*\{?\s*name\s*:')).Count )"
"json-ish      : $($body.TrimStart().StartsWith('{') -or $body.Trim().StartsWith('['))"
"html-ish      : $(($body -match '(?i)<html|<!doctype|<body').ToString())"
"count '<'     : $( ([regex]::Matches($body,'<')).Count )"
"count ':'     : $( ([regex]::Matches($body,':')).Count )"
"count '{'     : $( ([regex]::Matches($body,'\{')).Count )"
