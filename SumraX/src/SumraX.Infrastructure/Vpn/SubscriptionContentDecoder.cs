using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Extracts proxy links from subscription payloads (Base64, plain text, Clash YAML, JSON configs).
/// </summary>
public static class SubscriptionContentDecoder
{
    private static readonly Regex UriRegex = new(
        @"(?<![\w/])(vmess|vless|trojan-go|trojan|ss|ssr|hysteria2|hy2|hysteria|socks5|socks|tuic|wireguard)://[^\s""'<>]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<string> ExtractLinks(string content)
    {
        content = content.Trim().Trim('\uFEFF');
        if (string.IsNullOrWhiteSpace(content))
        {
            return [];
        }

        var links = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddLink(string? link)
        {
            if (string.IsNullOrWhiteSpace(link))
            {
                return;
            }

            link = link.Trim().Trim('"', '\'', ',', ';', ' ', '\t', '\r', '\n');
            if (link.Contains("://", StringComparison.Ordinal) && seen.Add(link))
            {
                links.Add(link);
            }
        }

        // Structured configs first
        if (content.StartsWith('{') || content.StartsWith('['))
        {
            foreach (var link in ExtractFromJson(content))
            {
                AddLink(link);
            }

            if (links.Count > 0)
            {
                return links;
            }
        }

        if (ClashProxyParser.LooksLikeClashYaml(content))
        {
            foreach (var profile in ClashProxyParser.ParseProxies(content))
            {
                var uri = ProxyLinkParser.ToShareLink(profile);
                if (!string.IsNullOrWhiteSpace(uri))
                {
                    AddLink(uri);
                }
            }

            if (links.Count > 0)
            {
                return links;
            }
        }

        // Decode nested Base64 (some providers double-encode)
        var decoded = TryDecodeNestedBase64(content);
        if (!ReferenceEquals(decoded, content))
        {
            foreach (var link in ExtractLinks(decoded))
            {
                AddLink(link);
            }

            if (links.Count > 0)
            {
                return links;
            }

            content = decoded;
        }

        // Line-based + regex scan (supports concatenated URIs without newlines)
        foreach (var rawLine in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (line.Contains("://", StringComparison.Ordinal))
            {
                foreach (var link in V2rayNSubscriptionParser.SplitConcatenatedShareLinks(line))
                {
                    AddLink(link);
                }

                continue;
            }

            foreach (Match match in UriRegex.Matches(line))
            {
                AddLink(match.Value);
            }
        }

        if (links.Count == 0)
        {
            foreach (var link in V2rayNSubscriptionParser.SplitConcatenatedShareLinks(content))
            {
                AddLink(link);
            }
        }

        if (links.Count == 0)
        {
            foreach (Match match in UriRegex.Matches(content))
            {
                AddLink(match.Value);
            }
        }

        return links;
    }

    private static IEnumerable<string> ExtractFromJson(string json)
    {
        var links = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            ExtractFromJsonElement(doc.RootElement, links);
        }
        catch
        {
            // not JSON
        }

        return links;
    }

    private static void ExtractFromJsonElement(JsonElement root, List<string> links)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var s = item.GetString();
                        if (s != null && s.Contains("://", StringComparison.Ordinal))
                        {
                            links.Add(s);
                        }
                    }
                    else if (item.ValueKind == JsonValueKind.Object)
                    {
                        var profile = SingBoxConfigParser.TryParseOutbound(item);
                        var uri = profile != null ? ProxyLinkParser.ToShareLink(profile) : null;
                        if (!string.IsNullOrWhiteSpace(uri))
                        {
                            links.Add(uri);
                        }
                    }
                }
                break;

            case JsonValueKind.Object:
                if (root.TryGetProperty("outbounds", out var outbounds) && outbounds.ValueKind == JsonValueKind.Array)
                {
                    foreach (var outbound in outbounds.EnumerateArray())
                    {
                        var profile = SingBoxConfigParser.TryParseOutbound(outbound);
                        var uri = profile != null ? ProxyLinkParser.ToShareLink(profile) : null;
                        if (!string.IsNullOrWhiteSpace(uri))
                        {
                            links.Add(uri);
                        }
                    }
                }

                if (root.TryGetProperty("proxies", out var proxies) && proxies.ValueKind == JsonValueKind.Array)
                {
                    foreach (var proxy in proxies.EnumerateArray())
                    {
                        var profile = ClashProxyParser.TryParseProxyObject(proxy);
                        var uri = profile != null ? ProxyLinkParser.ToShareLink(profile) : null;
                        if (!string.IsNullOrWhiteSpace(uri))
                        {
                            links.Add(uri);
                        }
                    }
                }

                if (root.TryGetProperty("add", out _))
                {
                    var profile = ProxyLinkParser.ParseVmessJson(root);
                    var uri = ProxyLinkParser.ToShareLink(profile);
                    if (!string.IsNullOrWhiteSpace(uri))
                    {
                        links.Add(uri);
                    }
                }

                break;
        }
    }

    internal static string TryDecodeNestedBase64(string input)
    {
        var current = input.Trim();
        for (var i = 0; i < 3; i++)
        {
            if (current.Contains("://", StringComparison.Ordinal) ||
                current.StartsWith('{') ||
                current.StartsWith('[') ||
                ClashProxyParser.LooksLikeClashYaml(current))
            {
                break;
            }

            if (!TryDecodeBase64Once(current, out var decoded))
            {
                break;
            }

            current = decoded;
        }

        return current;
    }

    internal static bool TryDecodeBase64Once(string input, out string decoded)
    {
        decoded = string.Empty;
        try
        {
            var trimmed = input.Trim().Replace("\r", "").Replace("\n", "");
            if (trimmed.Length < 8)
            {
                return false;
            }

            var bytes = Convert.FromBase64String(PadBase64(trimmed));
            decoded = Encoding.UTF8.GetString(bytes);
            return decoded.Length > 0;
        }
        catch
        {
            try
            {
                var urlSafe = input.Trim().Replace('-', '+').Replace('_', '/');
                var bytes = Convert.FromBase64String(PadBase64(urlSafe));
                decoded = Encoding.UTF8.GetString(bytes);
                return decoded.Length > 0;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static string PadBase64(string input) =>
        input.PadRight(input.Length + (4 - input.Length % 4) % 4, '=');
}
