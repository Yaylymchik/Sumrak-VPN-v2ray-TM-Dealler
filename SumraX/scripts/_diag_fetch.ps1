# Diagnostic: fetch subscription with several UAs, print metadata only (no tokens/bodies).
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

function Classify([string]$b) {
    if ([string]::IsNullOrWhiteSpace($b)) { return 'EMPTY' }
    $t = $b.Trim()
    if ($t.StartsWith('{') -or $t.StartsWith('[')) { return 'JSON' }
    if ($t -match '(?i)<!doctype|<html|<head|<body|<script') { return 'HTML' }
    if ($t -match '(?i)^(vless|vmess|trojan|ss|ssr|hysteria|hysteria2|hy2|tuic|wireguard|wg)://') { return 'PLAIN_LINKS' }
    # try base64
    try {
        $padded = $t.Replace('-','+').Replace('_','/')
        switch ($padded.Length % 4) { 2 { $padded += '==' } 3 { $padded += '=' } }
        $dec = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($padded))
        $links = ([regex]::Matches($dec, '(?im)^(vless|vmess|trojan|ss|ssr|hysteria2?|hy2|tuic|wg)://')).Count
        if ($links -gt 0) { return "BASE64_LINKS(n=$links)" }
        if ($dec -match '(?i)<html|<!doctype') { return 'BASE64_HTML' }
        if ($dec -match '(?i)proxies\s*:') { return 'BASE64_CLASH_YAML' }
        return 'BASE64_OTHER'
    } catch { }
    if ($t -match '(?i)proxies\s*:') { return 'CLASH_YAML' }
    return 'UNKNOWN_TEXT'
}

$uas = @(
    @{ n='Chrome-Win (app default)'; v='Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36' },
    @{ n='v2rayN/7.0'; v='v2rayN/7.0' },
    @{ n='Sumrak-Android'; v='2.2.21' }
)

foreach ($ua in $uas) {
    "=== UA: $($ua.n)"
    try {
        $r = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 30 -MaximumRedirection 5 -Headers @{ 'User-Agent' = $ua.v; 'Accept' = '*/*' }
        $body = $r.Content
        if ($r.Content -is [byte[]]) { $body = [Text.Encoding]::UTF8.GetString($r.Content) }
        "  status        : $($r.StatusCode)"
        "  content-type  : $($r.Headers['Content-Type'])"
        "  bytes         : $([Text.Encoding]::UTF8.GetByteCount($body))"
        "  shape         : $(Classify $body)"
        $ui = $r.Headers['subscription-userinfo']
        if ($ui) {
            $exp = [regex]::Match($ui, 'expire=(\d+)').Groups[1].Value
            $tot = [regex]::Match($ui, 'total=(\d+)').Groups[1].Value
            $up  = [regex]::Match($ui, 'upload=(\d+)').Groups[1].Value
            $dl  = [regex]::Match($ui, 'download=(\d+)').Groups[1].Value
            if ($exp -and [int64]$exp -gt 0) { $expTxt = ([datetimeoffset]::FromUnixTimeSeconds([int64]$exp)).ToString('yyyy-MM-dd') } else { $expTxt = 'none/0' }
            "  userinfo      : total=$tot upload=$up download=$dl expire=$expTxt"
        } else { "  userinfo      : <absent>" }
        "  update-hdr    : $($r.Headers['profile-update-interval'])"
    } catch {
        "  ERROR         : $($_.Exception.Message)"
        if ($_.Exception.Response) {
            "  status        : $([int]$_.Exception.Response.StatusCode)"
        }
    }
    ""
}
