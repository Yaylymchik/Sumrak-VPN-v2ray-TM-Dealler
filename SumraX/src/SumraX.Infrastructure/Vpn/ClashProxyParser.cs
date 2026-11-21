using System.Text.Json;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Parses Clash / Mihomo YAML proxy lists (proxies: section).
/// </summary>
public static class ClashProxyParser
{
    private static readonly HashSet<string> ProxyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ss", "vmess", "vless", "trojan", "hysteria", "hysteria2", "tuic", "wireguard", "socks5"
    };

    public static bool LooksLikeClashYaml(string content) =>
        content.Contains("proxies:", StringComparison.OrdinalIgnoreCase) &&
        (content.Contains("type:", StringComparison.OrdinalIgnoreCase) ||
         content.Contains("proxy-groups:", StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<ServerProfile> ParseProxies(string yaml)
    {
        var profiles = new List<ServerProfile>();
        var lines = yaml.Replace("\r\n", "\n").Split('\n');
        var inProxies = false;
        Dictionary<string, string>? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var trimmed = line.TrimStart();
            if (!inProxies)
            {
                if (trimmed.Equals("proxies:", StringComparison.OrdinalIgnoreCase))
                {
                    inProxies = true;
                }

                continue;
            }

            // Next top-level key ends proxies section
            if (!line.StartsWith(' ') && !line.StartsWith('\t') && trimmed.Contains(':'))
            {
                if (current != null)
                {
                    TryAddProxy(current, profiles);
                }

                break;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (current != null)
                {
                    TryAddProxy(current, profiles);
                }

                current = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var rest = trimmed[2..].Trim();
                if (rest.Contains(':'))
                {
                    ParseKeyValue(rest, current);
                }

                continue;
            }

            if (current != null && trimmed.Contains(':'))
            {
                ParseKeyValue(trimmed, current);
            }
        }

        if (current != null)
        {
            TryAddProxy(current, profiles);
        }

        return profiles;
    }

    public static ServerProfile? TryParseProxyObject(JsonElement proxy)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in proxy.EnumerateObject())
        {
            dict[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString() ?? "",
                JsonValueKind.Number => prop.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => ""
            };
        }

        var list = new List<ServerProfile>();
        TryAddProxy(dict, list);
        return list.FirstOrDefault();
    }

    private static void ParseKeyValue(string line, Dictionary<string, string> target)
    {
        var idx = line.IndexOf(':');
        if (idx <= 0)
        {
            return;
        }

        var key = line[..idx].Trim().Trim('"', '\'');
        var value = line[(idx + 1)..].Trim().Trim('"', '\'');
        target[key] = value;
    }

    private static void TryAddProxy(Dictionary<string, string> dict, List<ServerProfile> output)
    {
        if (!dict.TryGetValue("type", out var type) || !ProxyTypes.Contains(type))
        {
            return;
        }

        var profile = type.ToLowerInvariant() switch
        {
            "ss" => ParseSs(dict),
            "vmess" => ParseVmess(dict),
            "vless" => ParseVless(dict),
            "trojan" => ParseTrojan(dict),
            "hysteria" => ParseHysteria(dict),
            "hysteria2" => ParseHysteria2(dict),
            "tuic" => ParseTuic(dict),
            "wireguard" => ParseWireGuard(dict),
            "socks5" => ParseSocks5(dict),
            _ => null
        };

        if (profile != null && !string.IsNullOrWhiteSpace(profile.Address))
        {
            output.Add(profile);
        }
    }

    private static ServerProfile ParseSs(Dictionary<string, string> d) => new()
    {
        Protocol = VpnProtocol.Shadowsocks,
        Name = GetName(d, "Shadowsocks"),
        Address = d.GetValueOrDefault("server", ""),
        Port = ParseInt(d.GetValueOrDefault("port", "8388")),
        Encryption = d.GetValueOrDefault("cipher", d.GetValueOrDefault("method", "aes-256-gcm")),
        Password = d.GetValueOrDefault("password", ""),
        UserId = d.GetValueOrDefault("password", "")
    };

    private static ServerProfile ParseVmess(Dictionary<string, string> d)
    {
        var tls = d.GetValueOrDefault("tls", "false");
        var network = d.GetValueOrDefault("network", "tcp");
        return new ServerProfile
        {
            Protocol = VpnProtocol.VMess,
            Name = GetName(d, "VMess"),
            Address = d.GetValueOrDefault("server", ""),
            Port = ParseInt(d.GetValueOrDefault("port", "443")),
            UserId = d.GetValueOrDefault("uuid", ""),
            Encryption = d.GetValueOrDefault("cipher", "auto"),
            Network = network,
            Host = d.GetValueOrDefault("host", d.GetValueOrDefault("ws-headers", "")),
            Path = d.GetValueOrDefault("path", d.GetValueOrDefault("ws-path", "/")),
            Security = tls is "true" or "1" ? "tls" : "none",
            Sni = d.GetValueOrDefault("servername", d.GetValueOrDefault("sni", ""))
        };
    }

    private static ServerProfile ParseVless(Dictionary<string, string> d)
    {
        var tls = d.GetValueOrDefault("tls", "false");
        var reality = d.GetValueOrDefault("reality-opts", d.GetValueOrDefault("reality", "false"));
        var security = reality is "true" or "1" ? "reality" : tls is "true" or "1" ? "tls" : "none";
        return new ServerProfile
        {
            Protocol = VpnProtocol.VLESS,
            Name = GetName(d, "VLESS"),
            Address = d.GetValueOrDefault("server", ""),
            Port = ParseInt(d.GetValueOrDefault("port", "443")),
            UserId = d.GetValueOrDefault("uuid", ""),
            Flow = d.GetValueOrDefault("flow", ""),
            Network = d.GetValueOrDefault("network", "tcp"),
            Host = d.GetValueOrDefault("host", ""),
            Path = d.GetValueOrDefault("path", "/"),
            Security = security,
            Sni = d.GetValueOrDefault("servername", d.GetValueOrDefault("sni", "")),
            Fingerprint = d.GetValueOrDefault("client-fingerprint", "chrome")
        };
    }

    private static ServerProfile ParseTrojan(Dictionary<string, string> d)
    {
        var tls = d.GetValueOrDefault("tls", "true");
        return new ServerProfile
        {
            Protocol = VpnProtocol.Trojan,
            Name = GetName(d, "Trojan"),
            Address = d.GetValueOrDefault("server", ""),
            Port = ParseInt(d.GetValueOrDefault("port", "443")),
            Password = d.GetValueOrDefault("password", ""),
            UserId = d.GetValueOrDefault("password", ""),
            Security = tls is "false" or "0" ? "none" : "tls",
            Sni = d.GetValueOrDefault("sni", d.GetValueOrDefault("servername", "")),
            Network = d.GetValueOrDefault("network", "tcp"),
            Host = d.GetValueOrDefault("host", ""),
            Path = d.GetValueOrDefault("path", "/")
        };
    }

    private static ServerProfile ParseHysteria(Dictionary<string, string> d) => new()
    {
        Protocol = VpnProtocol.Hysteria,
        Name = GetName(d, "Hysteria"),
        Address = d.GetValueOrDefault("server", ""),
        Port = ParseInt(d.GetValueOrDefault("port", "443")),
        Password = d.GetValueOrDefault("auth-str", d.GetValueOrDefault("auth", "")),
        UserId = d.GetValueOrDefault("auth-str", d.GetValueOrDefault("auth", "")),
        Sni = d.GetValueOrDefault("sni", ""),
        TransportMode = d.GetValueOrDefault("up", d.GetValueOrDefault("upmbps", "")),
        Alpn = d.GetValueOrDefault("down", d.GetValueOrDefault("downmbps", "")),
        Security = "tls"
    };

    private static ServerProfile ParseHysteria2(Dictionary<string, string> d) => new()
    {
        Protocol = VpnProtocol.Hysteria2,
        Name = GetName(d, "Hysteria2"),
        Address = d.GetValueOrDefault("server", ""),
        Port = ParseInt(d.GetValueOrDefault("port", "443")),
        Password = d.GetValueOrDefault("password", d.GetValueOrDefault("auth", "")),
        UserId = d.GetValueOrDefault("password", d.GetValueOrDefault("auth", "")),
        Sni = d.GetValueOrDefault("sni", ""),
        Security = "tls"
    };

    private static ServerProfile ParseTuic(Dictionary<string, string> d) => new()
    {
        Protocol = VpnProtocol.Tuic,
        Name = GetName(d, "TUIC"),
        Address = d.GetValueOrDefault("server", ""),
        Port = ParseInt(d.GetValueOrDefault("port", "443")),
        UserId = d.GetValueOrDefault("uuid", ""),
        Password = d.GetValueOrDefault("password", d.GetValueOrDefault("uuid", "")),
        Sni = d.GetValueOrDefault("sni", d.GetValueOrDefault("servername", "")),
        Alpn = d.GetValueOrDefault("alpn", "h3"),
        TransportMode = d.GetValueOrDefault("congestion-controller", d.GetValueOrDefault("congestion_control", "bbr")),
        Security = "tls"
    };

    private static ServerProfile ParseWireGuard(Dictionary<string, string> d)
    {
        var extra = XrayConfigBuilder.WireGuardExtra.Serialize(new XrayConfigBuilder.WireGuardExtra
        {
            SecretKey = d.GetValueOrDefault("private-key", d.GetValueOrDefault("privateKey", "")),
            PublicKey = d.GetValueOrDefault("public-key", d.GetValueOrDefault("publicKey", "")),
            Addresses = d.GetValueOrDefault("ip", d.GetValueOrDefault("address", "10.0.0.2/32"))
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Mtu = ParseInt(d.GetValueOrDefault("mtu", "1420")),
            KeepAlive = ParseInt(d.GetValueOrDefault("keepalive", "25"))
        });

        return new ServerProfile
        {
            Protocol = VpnProtocol.WireGuard,
            Name = GetName(d, "WireGuard"),
            Address = d.GetValueOrDefault("server", ""),
            Port = ParseInt(d.GetValueOrDefault("port", "51820")),
            Extra = extra
        };
    }

    private static ServerProfile ParseSocks5(Dictionary<string, string> d) => new()
    {
        Protocol = VpnProtocol.Socks,
        Name = GetName(d, "SOCKS5"),
        Address = d.GetValueOrDefault("server", ""),
        Port = ParseInt(d.GetValueOrDefault("port", "1080")),
        UserId = d.GetValueOrDefault("username", ""),
        Password = d.GetValueOrDefault("password", "")
    };

    private static string GetName(Dictionary<string, string> d, string fallback) =>
        d.GetValueOrDefault("name", fallback);

    private static int ParseInt(string value) =>
        int.TryParse(value, out var port) ? port : 443;
}
