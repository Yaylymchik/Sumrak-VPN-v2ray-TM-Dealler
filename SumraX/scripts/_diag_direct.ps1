# Compares direct vs system-proxy fetch. Prints only status/size/time.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security

$path = Join-Path $env:APPDATA 'Sumrak VPN\settings.json'
$raw = (Get-Content $path -Raw).Trim()
$entropy = [Text.Encoding]::UTF8.GetBytes('SumraX.VPN.v1')
$plain = [Security.Cryptography.ProtectedData]::Unprotect([Convert]::FromBase64String($raw), $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
$url = (@(([Text.Encoding]::UTF8.GetString($plain) | ConvertFrom-Json).SubscriptionUrls))[0]

"direct (no proxy):"
& curl.exe -s -o NUL -w "  code=%{http_code} bytes=%{size_download} time=%{time_total}s\n" --noproxy "*" --max-time 30 -A "v2rayN/7.0" $url

"via system proxy 127.0.0.1:10809:"
& curl.exe -s -o NUL -w "  code=%{http_code} bytes=%{size_download} time=%{time_total}s\n" -x "http://127.0.0.1:10809" --max-time 30 -A "v2rayN/7.0" $url
