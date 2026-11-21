using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// v2rayN-compatible subscription parsing (base64 link lists, SSD, SIP008, wireguard ini, concatenated URIs).
/// </summary>
public static class V2rayNSubscriptionParser
{
    private static readonly Regex SchemeStartRegex = new(
        @"(vmess|vless|trojan-go|trojan|ss|ssr|hysteria2|hy2|hysteria|socks5|socks|tuic|wireguard|wg|anytls)://",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<ServerProfile> Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        foreach (var body in BuildCandidateBodies(raw))
        {
            var profiles = TryParseBody(body);
            if (profiles.Count > 0)
            {
                return profiles;
            }
        }

        return [];
    }

    private static IEnumerable<string> BuildCandidateBodies(string raw)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>();
        void Enqueue(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                return;
            }

            s = s.Trim().Trim('\uFEFF');
            if (seen.Add(s))
            {
                queue.Enqueue(s);
            }
        }

        Enqueue(raw);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            if (current.Contains("://", StringComparison.Ordinal) ||
                current.StartsWith('{') ||
                current.StartsWith('[') ||
                ClashProxyParser.LooksLikeClashYaml(current) ||
                LooksLikeWireGuardIni(current))
            {
                continue;
            }

            if (SubscriptionContentDecoder.TryDecodeBase64Once(current, out var once) &&
                !string.Equals(once, current, StringComparison.Ordinal))
            {
                Enqueue(once);
            }

            var nested = SubscriptionContentDecoder.TryDecodeNestedBase64(current);
            if (!string.Equals(nested, current, StringComparison.Ordinal))
            {
                Enqueue(nested);
            }
        }
    }

    private static List<ServerProfile> TryParseBody(string body)
    {
        var profiles = ParseShareLinks(body);
        if (profiles.Count > 0)
        {
            return profiles;
        }

        profiles = ParseSip008(body);
        if (profiles.Count > 0)
        {
            return profiles;
        }

        profiles = ParseSsd(body);
        if (profiles.Count > 0)
        {
            return profiles;
        }

        profiles = ParseWireGuardIni(body);
        if (profiles.Count > 0)
        {
            return profiles;
        }

        return ParseStructuredJsonConfig(body);
    }

    private static List<ServerProfile> ParseShareLinks(string body)
    {
        var servers = new List<ServerProfile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in ExtractAllShareLinks(body))
        {
            var profile = ProxyLinkParser.TryParse(link);
            if (profile == null || string.IsNullOrWhiteSpace(profile.Address))
            {
                continue;
            }

            if (profile.Address is "127.0.0.1" or "localhost" or "::1")
            {
                continue;
            }

            var key = $"{profile.Protocol}|{profile.Address}|{profile.Port}|{profile.UserId}|{profile.Name}";
            if (!seen.Add(key))
            {
                continue;
            }

            profile.RawConfig = link;
            servers.Add(profile);
        }

        return servers;
    }

    internal static IEnumerable<string> ExtractAllShareLinks(string content)
    {
        content = content.Trim().Trim('\uFEFF');
        if (string.IsNullOrWhiteSpace(content))
        {
            yield break;
        }

        if (content.StartsWith("ssd://", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        if (ClashProxyParser.LooksLikeClashYaml(content))
        {
            foreach (var profile in ClashProxyParser.ParseProxies(content))
            {
                var uri = ProxyLinkParser.ToShareLink(profile);
                if (!string.IsNullOrWhiteSpace(uri))
                {
                    yield return uri;
                }
            }

            yield break;
        }

        if (content.StartsWith('{') || content.StartsWith('['))
        {
            foreach (var link in ExtractLinksFromJson(content))
            {
                yield return link;
            }

            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!trimmed.Contains("://", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var link in SplitConcatenatedShareLinks(trimmed))
            {
                if (seen.Add(link))
                {
                    yield return link;
                }
            }
        }

        if (seen.Count > 0)
        {
            yield break;
        }

        foreach (var link in SplitConcatenatedShareLinks(content))
        {
            if (seen.Add(link))
            {
                yield return link;
            }
        }
    }

    internal static IEnumerable<string> SplitConcatenatedShareLinks(string text)
    {
        var matches = SchemeStartRegex.Matches(text);
        if (matches.Count == 0)
        {
            if (text.Contains("://", StringComparison.Ordinal))
            {
                yield return text.Trim().Trim('"', '\'', ',', ';');
            }

            yield break;
        }

        for (var i = 0; i < matches.Count; i++)
        {
            var start = matches[i].Index;
            var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
            var link = text[start..end].Trim().Trim('"', '\'', ',', ';', ' ');
            if (link.Contains("://", StringComparison.Ordinal))
            {
                yield return link;
            }
        }
    }

    private static IEnumerable<string> ExtractLinksFromJson(string json)
    {
        var links = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            void Walk(JsonElement el)
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.String:
                        var s = el.GetString();
                        if (s != null && ProxyLinkParser.IsSupportedScheme(s))
                        {
                            links.Add(s);
                        }
                        break;
                    case JsonValueKind.Array:
                        foreach (var item in el.EnumerateArray())
                        {
                            Walk(item);
                        }
                        break;
                    case JsonValueKind.Object:
                        if (el.TryGetProperty("add", out _))
                        {
                            var uri = ProxyLinkParser.ToShareLink(ProxyLinkParser.ParseVmessJson(el));
                            if (!string.IsNullOrWhiteSpace(uri))
                            {
                                links.Add(uri);
                            }
                        }

                        foreach (var prop in el.EnumerateObject())
                        {
                            Walk(prop.Value);
                        }
                        break;
                }
            }

            Walk(doc.RootElement);
        }
        catch
        {
            // ignore
        }

        return links.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static List<ServerProfile> ParseSip008(string body)
    {
        var result = new List<ServerProfile>();
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in servers.EnumerateArray())
            {
                var host = GetString(item, "server") ?? GetString(item, "host");
                if (string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                result.Add(new ServerProfile
                {
                    Protocol = VpnProtocol.Shadowsocks,
                    Name = GetString(item, "remarks") ?? GetString(item, "name") ?? host,
                    Address = host,
                    Port = GetInt(item, "server_port", 8388),
                    Encryption = GetString(item, "method") ?? "aes-256-gcm",
                    Password = GetString(item, "password") ?? "",
                    RawConfig = item.GetRawText()
                });
            }
        }
        catch
        {
            // not sip008
        }

        return result;
    }

    private static List<ServerProfile> ParseSsd(string body)
    {
        var result = new List<ServerProfile>();
        try
        {
            var payload = body.Trim();
            if (payload.StartsWith("ssd://", StringComparison.OrdinalIgnoreCase))
            {
                payload = payload[6..];
            }
            else if (!payload.StartsWith('{'))
            {
                if (!SubscriptionContentDecoder.TryDecodeBase64Once(payload, out var decoded))
                {
                    return result;
                }

                payload = decoded;
            }

            if (payload.StartsWith("ssd://", StringComparison.OrdinalIgnoreCase))
            {
                payload = payload[6..];
            }

            if (!payload.TrimStart().StartsWith('{'))
            {
                if (!SubscriptionContentDecoder.TryDecodeBase64Once(payload, out var decodedJson))
                {
                    return result;
                }

                payload = decodedJson;
            }

            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            var port = GetInt(root, "port", 443);
            var encryption = GetString(root, "encryption") ?? "aes-256-gcm";
            var password = GetString(root, "password") ?? "";
            var airport = GetString(root, "airport") ?? "SSD";

            if (!root.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            var index = 0;
            foreach (var entry in servers.EnumerateArray())
            {
                index++;
                string? host = null;
                string? name = null;
                if (entry.ValueKind == JsonValueKind.Array)
                {
                    var arr = entry.EnumerateArray().ToList();
                    if (arr.Count > 0)
                    {
                        host = arr[0].GetString();
                    }

                    if (arr.Count > 1)
                    {
                        name = arr[1].GetString();
                    }
                }
                else if (entry.ValueKind == JsonValueKind.String)
                {
                    host = entry.GetString();
                }
                else if (entry.ValueKind == JsonValueKind.Object)
                {
                    host = GetString(entry, "server") ?? GetString(entry, "host");
                    name = GetString(entry, "remarks") ?? GetString(entry, "name");
                    port = GetInt(entry, "port", port);
                }

                if (string.IsNullOrWhiteSpace(host))
                {
                    continue;
                }

                result.Add(new ServerProfile
                {
                    Protocol = VpnProtocol.Shadowsocks,
                    Name = name ?? $"{airport} #{index}",
                    Address = host,
                    Port = port,
                    Encryption = encryption,
                    Password = password,
                    RawConfig = entry.GetRawText()
                });
            }
        }
        catch
        {
            // not ssd
        }

        return result;
    }

    private static List<ServerProfile> ParseWireGuardIni(string body)
    {
        if (!LooksLikeWireGuardIni(body))
        {
            return [];
        }

        var result = new List<ServerProfile>();
        string? endpoint = null;
        string? name = "WireGuard";
        foreach (var rawLine in body.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.Trim();
            if (line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('['))
            {
                continue;
            }

            var eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (key.Equals("Endpoint", StringComparison.OrdinalIgnoreCase))
            {
                endpoint = value;
            }
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return result;
        }

        var hostPart = endpoint;
        var port = 51820;
        if (endpoint.Contains(':'))
        {
            var lastColon = endpoint.LastIndexOf(':');
            hostPart = endpoint[..lastColon];
            if (int.TryParse(endpoint[(lastColon + 1)..], out var p))
            {
                port = p;
            }
        }

        hostPart = hostPart.Trim('[', ']');
        result.Add(new ServerProfile
        {
            Protocol = VpnProtocol.WireGuard,
            Name = name,
            Address = hostPart,
            Port = port,
            RawConfig = body
        });

        return result;
    }

    private static List<ServerProfile> ParseStructuredJsonConfig(string body)
    {
        var profiles = new List<ServerProfile>();
        if (!body.TrimStart().StartsWith('{') && !body.TrimStart().StartsWith('['))
        {
            return profiles;
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            CollectJsonProfiles(doc.RootElement, profiles);
        }
        catch
        {
            // ignore
        }

        return profiles;
    }

    private static void CollectJsonProfiles(JsonElement root, List<ServerProfile> profiles)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    CollectJsonProfiles(item, profiles);
                }
                break;
            case JsonValueKind.Object:
                if (root.TryGetProperty("outbounds", out var outbounds))
                {
                    foreach (var outbound in outbounds.EnumerateArray())
                    {
                        var profile = SingBoxConfigParser.TryParseOutbound(outbound);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                }

                if (root.TryGetProperty("proxies", out var proxies))
                {
                    foreach (var proxy in proxies.EnumerateArray())
                    {
                        var profile = ClashProxyParser.TryParseProxyObject(proxy);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                }

                if (root.TryGetProperty("add", out _))
                {
                    profiles.Add(ProxyLinkParser.ParseVmessJson(root));
                }

                break;
        }
    }

    private static bool LooksLikeWireGuardIni(string body) =>
        body.Contains("[Interface]", StringComparison.OrdinalIgnoreCase) &&
        body.Contains("[Peer]", StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;

    private static int GetInt(JsonElement el, string name, int fallback)
    {
        if (!el.TryGetProperty(name, out var prop))
        {
            return fallback;
        }

        return prop.ValueKind switch
        {
            JsonValueKind.Number when prop.TryGetInt32(out var n) => n,
            JsonValueKind.String when int.TryParse(prop.GetString(), out var s) => s,
            _ => fallback
        };
    }
}
