# Diagnostics only: never prints subscription tokens or full URLs.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security

$path = Join-Path $env:APPDATA 'Sumrak VPN\settings.json'
$raw = Get-Content $path -Raw
$entropy = [Text.Encoding]::UTF8.GetBytes('SumraX.VPN.v1')

try {
    $bytes = [Convert]::FromBase64String($raw.Trim())
    $plain = [Security.Cryptography.ProtectedData]::Unprotect(
        $bytes, $entropy, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    $json = [Text.Encoding]::UTF8.GetString($plain)
} catch {
    'DECRYPT FAILED: ' + $_.Exception.Message
    exit 1
}

$j = $json | ConvertFrom-Json

"AutoRefreshSubscriptions      : $($j.AutoRefreshSubscriptions)"
"SubscriptionRefreshIntervalH  : $($j.SubscriptionRefreshIntervalHours)"
"LastSubscriptionRefreshUtc    : $($j.LastSubscriptionRefreshUtc)"
"SubscriptionUserAgent         : $($j.SubscriptionUserAgent)"
"IgnoreDuplicateServers        : $($j.IgnoreDuplicateServers)"
"PurgeOldProfilesOnUpdate      : $($j.PurgeOldProfilesOnSubscriptionUpdate)"
""

$urls = @($j.SubscriptionUrls)
"SubscriptionUrls count        : $($urls.Count)"
$i = 0
foreach ($u in $urls) {
    $i++
    if ([string]::IsNullOrWhiteSpace($u)) { "  [$i] <empty>"; continue }
    try {
        $uri = [Uri]$u
        $hostPart = "$($uri.Scheme)://$($uri.Host)"
        $seg = ($uri.AbsolutePath -split '/' | Where-Object { $_ }).Count
        "  [$i] $hostPart  pathSegments=$seg  totalLen=$($u.Length)  trailingSpace=$($u -match '\s$')"
    } catch {
        "  [$i] unparsable, len=$($u.Length)"
    }
}

""
"--- profile/subscription hints in settings ---"
foreach ($p in $j.PSObject.Properties.Name) {
    if ($p -match 'Subscri|Refresh|Url') { "  field: $p" }
}
