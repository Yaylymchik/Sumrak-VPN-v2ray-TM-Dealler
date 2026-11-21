using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SumraX.Core.Enums;
using SumraX.Core.Models;
using SumraX.Core.Util;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Parses individual proxy share links (vmess://, vless://, trojan://, ss://, ssr://, hysteria2://, etc.).
/// </summary>
public static class ProxyLinkParser
{
    private static readonly string[] SupportedSchemes =
    [
        "vmess://", "vless://", "trojan://", "trojan-go://", "ss://", "ssr://",
        "hysteria://", "hysteria2://", "hy2://",
        "socks://", "socks5://", "socks4://",
        "wireguard://", "wg://", "tuic://"
    ];

    public static bool IsSupportedScheme(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return false;
        }

        link = link.Trim();
        foreach (var scheme in SupportedSchemes)
        {
            if (link.StartsWith(scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return link.StartsWith('{') || link.StartsWith('[');
    }

    public static ServerProfile? TryParse(string link)
    {
        link = link.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        if (link.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            link.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        try
        {
            ServerProfile? profile = link switch
            {
                _ when link.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase) =>
                    ParseVmess(link[8..]),
                _ when link.StartsWith("vless://", StringComparison.OrdinalIgnoreCase) =>
                    ParseUriLink(link, VpnProtocol.VLESS),
                _ when link.StartsWith("trojan-go://", StringComparison.OrdinalIgnoreCase) =>
                    ParseUriLink("trojan://" + link[12..], VpnProtocol.Trojan),
                _ when link.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase) =>
                    ParseUriLink(link, VpnProtocol.Trojan),
                _ when link.StartsWith("ss://", StringComparison.OrdinalIgnoreCase) =>
                    ParseShadowsocks(link),
                _ when link.StartsWith("ssr://", StringComparison.OrdinalIgnoreCase) =>
                    ParseShadowsocksR(link),
                _ when link.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase) =>
                    ParseHysteria2Uri(link[12..]),
                _ when link.StartsWith("hy2://", StringComparison.OrdinalIgnoreCase) =>
                    ParseHysteria2Uri(link[6..]),
                _ when link.StartsWith("hysteria://", StringComparison.OrdinalIgnoreCase) =>
                    ParseHysteria2Uri(link[11..], legacy: true),
                _ when link.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase) ||
                     link.StartsWith("socks4://", StringComparison.OrdinalIgnoreCase) ||
                     link.StartsWith("socks://", StringComparison.OrdinalIgnoreCase) =>
                    ParseSocks(link),
                _ when link.StartsWith("wireguard://", StringComparison.OrdinalIgnoreCase) ||
                     link.StartsWith("wg://", StringComparison.OrdinalIgnoreCase) =>
                    ParseWireGuard(link),
                _ when link.StartsWith("tuic://", StringComparison.OrdinalIgnoreCase) =>
                    ParseTuic(link),
                _ when link.StartsWith('{') || link.StartsWith('[') =>
                    ParseJsonLink(link),
                _ => null
            };

            if (profile != null)
            {
                ServerDisplayName.ApplyToProfile(profile);
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }

    public static string? ToShareLink(ServerProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Address))
        {
            return null;
        }

        return profile.Protocol switch
        {
            VpnProtocol.VMess => BuildVmessLink(profile),
            VpnProtocol.VLESS => BuildVlessLink(profile),
            VpnProtocol.Trojan => BuildTrojanLink(profile),
            VpnProtocol.Shadowsocks => BuildSsLink(profile),
            VpnProtocol.Hysteria2 => BuildHy2Link(profile),
            VpnProtocol.Socks or VpnProtocol.WireGuard or VpnProtocol.Tuic or VpnProtocol.ShadowsocksR =>
                string.IsNullOrWhiteSpace(profile.RawConfig) ? null : profile.RawConfig,
            _ => null
        };
    }

    public static ServerProfile ParseVmessJson(JsonElement el) => new()
    {
        Protocol = VpnProtocol.VMess,
        Name = el.TryGetProperty("ps", out var ps) ? ps.GetString() ?? "VMess Server" : "VMess Server",
        Address = el.GetProperty("add").GetString() ?? "",
        Port = el.TryGetProperty("port", out var port) ? ParsePort(port) : 443,
        UserId = el.GetProperty("id").GetString() ?? "",
        Encryption = el.TryGetProperty("scy", out var scy) ? scy.GetString() ?? "auto" : "auto",
        Network = el.TryGetProperty("net", out var net) ? net.GetString() ?? "tcp" : "tcp",
        Host = el.TryGetProperty("host", out var host) ? host.GetString() ?? "" : "",
        Path = el.TryGetProperty("path", out var path) ? path.GetString() ?? "" : "",
        Security = el.TryGetProperty("tls", out var tls) && tls.GetString() == "tls" ? "tls" : "none",
        Sni = el.TryGetProperty("sni", out var sni) ? sni.GetString() ?? "" : ""
    };

    private static ServerProfile? ParseJsonLink(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("add", out _))
            {
                return ParseVmessJson(root);
            }

            return SingBoxConfigParser.TryParseOutbound(root);
        }
        catch
        {
            return null;
        }
    }

    private static ServerProfile? ParseVmess(string payload)
    {
        payload = payload.Trim();
        if (payload.Contains('@') && !payload.StartsWith('{'))
        {
            return ParseVmessShadowRocket(payload);
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(SubscriptionContentDecoder.PadBase64(payload)));
        using var doc = JsonDocument.Parse(json);
        return ParseVmessJson(doc.RootElement);
    }

    private static ServerProfile? ParseVmessShadowRocket(string payload)
    {
        var qIdx = payload.IndexOf('?');
        var main = qIdx >= 0 ? payload[..qIdx] : payload;
        var query = qIdx >= 0 ? payload[(qIdx + 1)..] : "";
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(SubscriptionContentDecoder.PadBase64(main)));
        var at = decoded.LastIndexOf('@');
        if (at <= 0)
        {
            return null;
        }

        var creds = decoded[..at].Split(':', 2);
        var hostPort = decoded[(at + 1)..];
        var colon = hostPort.LastIndexOf(':');
        var host = colon > 0 ? hostPort[..colon] : hostPort;
        var port = colon > 0 && int.TryParse(hostPort[(colon + 1)..], out var p) ? p : 443;
        var q = ParseQuery(query);

        return new ServerProfile
        {
            Protocol = VpnProtocol.VMess,
            Name = q.GetValueOrDefault("remarks", q.GetValueOrDefault("remark", "VMess Server")),
            Address = host,
            Port = port,
            UserId = creds.Length > 1 ? creds[1] : creds[0],
            Encryption = creds.Length > 1 ? creds[0] : "auto",
            Network = q.GetValueOrDefault("obfs", "tcp"),
            Security = q.GetValueOrDefault("tls", "0") is "1" or "true" ? "tls" : "none"
        };
    }

    private static ServerProfile ParseUriLink(string link, VpnProtocol protocol)
    {
        var uri = new Uri(link);
        var query = ParseQuery(uri.Query);

        var security = query.GetValueOrDefault("security", "none");
        if (security is "" or "none" && query.ContainsKey("tls"))
        {
            security = query["tls"] is "1" or "true" ? "tls" : "none";
        }

        if (query.ContainsKey("pbk") || query.ContainsKey("sid"))
        {
            security = "reality";
        }

        var profile = new ServerProfile
        {
            Protocol = protocol,
            Name = string.IsNullOrWhiteSpace(uri.Fragment.TrimStart('#'))
                ? $"{protocol} Server"
                : Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 443,
            UserId = Uri.UnescapeDataString(uri.UserInfo),
            Password = Uri.UnescapeDataString(uri.UserInfo),
            Security = security is "tls" or "reality" ? security : "none",
            Network = query.GetValueOrDefault("type", query.GetValueOrDefault("network", "tcp")),
            Host = query.GetValueOrDefault("host", ""),
            Path = query.GetValueOrDefault("path", "/"),
            Sni = query.GetValueOrDefault("sni", query.GetValueOrDefault("peer", query.GetValueOrDefault("host", ""))),
            Encryption = query.GetValueOrDefault("encryption", protocol == VpnProtocol.VLESS ? "none" : "auto"),
            Flow = query.GetValueOrDefault("flow", ""),
            Fingerprint = query.GetValueOrDefault("fp", query.GetValueOrDefault("fingerprint", "chrome")),
            Alpn = query.GetValueOrDefault("alpn", ""),
            TransportMode = query.GetValueOrDefault("mode", ""),
            Extra = BuildRealityExtra(query)
        };

        if (string.IsNullOrWhiteSpace(profile.Path) && profile.Network is "ws" or "xhttp")
        {
            profile.Path = "/";
        }

        return profile;
    }

    private static string BuildRealityExtra(Dictionary<string, string> query)
    {
        if (!query.ContainsKey("pbk") && !query.ContainsKey("sid"))
        {
            return query.GetValueOrDefault("extra", "");
        }

        var obj = new Dictionary<string, string>();
        if (query.TryGetValue("pbk", out var pbk)) obj["publicKey"] = pbk;
        if (query.TryGetValue("sid", out var sid)) obj["shortId"] = sid;
        if (query.TryGetValue("spx", out var spx)) obj["spiderX"] = spx;
        return JsonSerializer.Serialize(obj);
    }

    private static ServerProfile? ParseShadowsocks(string link)
    {
        var body = link[5..];
        string method, password, host;
        int port;
        string name = "Shadowsocks Server";

        var hashIdx = body.IndexOf('#');
        if (hashIdx >= 0)
        {
            name = Uri.UnescapeDataString(body[(hashIdx + 1)..]);
            body = body[..hashIdx];
        }

        if (body.Contains('@'))
        {
            var uri = new Uri("ss://" + body);
            var userInfo = uri.UserInfo;
            if (userInfo.Contains('%'))
            {
                userInfo = Uri.UnescapeDataString(userInfo);
            }

            if (!userInfo.Contains(':') && userInfo.Length > 10)
            {
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(SubscriptionContentDecoder.PadBase64(userInfo)));
                var parts = decoded.Split(':', 2);
                method = parts[0];
                password = parts.Length > 1 ? parts[1] : "";
            }
            else
            {
                var parts = userInfo.Split(':', 2);
                method = parts[0];
                password = parts.Length > 1 ? parts[1] : "";
            }

            host = uri.Host;
            port = uri.Port;
        }
        else
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(SubscriptionContentDecoder.PadBase64(body)));
            if (decoded.Contains('@'))
            {
                var at = decoded.LastIndexOf('@');
                var creds = decoded[..at].Split(':', 2);
                method = creds[0];
                password = creds.Length > 1 ? creds[1] : "";
                var hostPort = decoded[(at + 1)..];
                var colon = hostPort.LastIndexOf(':');
                host = colon > 0 ? hostPort[..colon] : hostPort;
                port = colon > 0 ? int.Parse(hostPort[(colon + 1)..]) : 8388;
            }
            else
            {
                return null;
            }
        }

        return new ServerProfile
        {
            Protocol = VpnProtocol.Shadowsocks,
            Name = name,
            Address = host,
            Port = port,
            Encryption = method,
            Password = password,
            UserId = password
        };
    }

    private static ServerProfile? ParseShadowsocksR(string link)
    {
        var payload = link[6..];
        var hashIdx = payload.IndexOf('#');
        if (hashIdx >= 0)
        {
            payload = payload[..hashIdx];
        }

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(SubscriptionContentDecoder.PadBase64(payload)));
        var qIdx = decoded.IndexOf("/?", StringComparison.Ordinal);
        var main = qIdx >= 0 ? decoded[..qIdx] : decoded;
        var queryPart = qIdx >= 0 ? decoded[(qIdx + 2)..] : "";
        var parts = main.Split(':');
        if (parts.Length < 6)
        {
            return null;
        }

        var host = parts[0];
        var port = int.TryParse(parts[1], out var p) ? p : 443;
        var protocol = parts[2];
        var method = parts[3];
        var obfs = parts[4];
        var passB64 = parts[5];
        var password = TryDecodeBase64Field(passB64);

        var q = ParseQuery(queryPart);
        var name = q.TryGetValue("remarks", out var remarks) ? TryDecodeBase64Field(remarks) : "SSR Server";
        var obfsParam = q.TryGetValue("obfsparam", out var op) ? TryDecodeBase64Field(op) : "";
        var protocolParam = q.TryGetValue("protoparam", out var pp) ? TryDecodeBase64Field(pp) : "";

        if (protocol.Equals("origin", StringComparison.OrdinalIgnoreCase) &&
            obfs.Equals("plain", StringComparison.OrdinalIgnoreCase))
        {
            return new ServerProfile
            {
                Protocol = VpnProtocol.Shadowsocks,
                Name = name,
                Address = host,
                Port = port,
                Encryption = method,
                Password = password,
                UserId = password
            };
        }

        return new ServerProfile
        {
            Protocol = VpnProtocol.ShadowsocksR,
            Name = name,
            Address = host,
            Port = port,
            Encryption = method,
            Password = password,
            UserId = password,
            Extra = SingBoxConfigBuilder.SsrExtra.Serialize(protocol, obfs, obfsParam, protocolParam)
        };
    }

    private static ServerProfile ParseSocks(string link)
    {
        var uri = new Uri(link);
        var user = "";
        var pass = "";
        if (!string.IsNullOrWhiteSpace(uri.UserInfo))
        {
            var parts = uri.UserInfo.Split(':', 2);
            user = Uri.UnescapeDataString(parts[0]);
            pass = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "";
        }

        return new ServerProfile
        {
            Protocol = VpnProtocol.Socks,
            Name = string.IsNullOrWhiteSpace(uri.Fragment.TrimStart('#'))
                ? "SOCKS Proxy"
                : Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 1080,
            UserId = user,
            Password = pass
        };
    }

    private static ServerProfile ParseWireGuard(string link)
    {
        var uri = new Uri(link);
        var query = ParseQuery(uri.Query);
        var secretKey = Uri.UnescapeDataString(uri.UserInfo);
        if (string.IsNullOrWhiteSpace(secretKey) && query.TryGetValue("privateKey", out var pk))
        {
            secretKey = pk;
        }

        var addresses = new List<string>();
        if (query.TryGetValue("address", out var addr))
        {
            addresses.AddRange(addr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        var reserved = Array.Empty<int>();
        if (query.TryGetValue("reserved", out var res) && !string.IsNullOrWhiteSpace(res))
        {
            reserved = res.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.Parse(s.Trim()))
                .ToArray();
        }

        var extra = XrayConfigBuilder.WireGuardExtra.Serialize(new XrayConfigBuilder.WireGuardExtra
        {
            SecretKey = secretKey,
            PublicKey = query.GetValueOrDefault("publickey", query.GetValueOrDefault("publicKey", "")),
            Endpoint = query.GetValueOrDefault("endpoint", ""),
            Addresses = addresses,
            Reserved = reserved,
            Mtu = int.TryParse(query.GetValueOrDefault("mtu", "0"), out var mtu) ? mtu : 0,
            KeepAlive = int.TryParse(query.GetValueOrDefault("keepalive", "0"), out var ka) ? ka : 25
        });

        return new ServerProfile
        {
            Protocol = VpnProtocol.WireGuard,
            Name = string.IsNullOrWhiteSpace(uri.Fragment.TrimStart('#'))
                ? "WireGuard"
                : Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 51820,
            Extra = extra
        };
    }

    private static ServerProfile ParseTuic(string link)
    {
        var uri = new Uri(link);
        var query = ParseQuery(uri.Query);
        var creds = uri.UserInfo.Split(':', 2);
        var uuid = Uri.UnescapeDataString(creds[0]);
        var password = creds.Length > 1 ? Uri.UnescapeDataString(creds[1]) : uuid;

        return new ServerProfile
        {
            Protocol = VpnProtocol.Tuic,
            Name = string.IsNullOrWhiteSpace(uri.Fragment.TrimStart('#'))
                ? "TUIC"
                : Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 443,
            UserId = uuid,
            Password = password,
            Sni = query.GetValueOrDefault("sni", query.GetValueOrDefault("peer", "")),
            Alpn = query.GetValueOrDefault("alpn", "h3"),
            TransportMode = query.GetValueOrDefault("congestion_control", "bbr"),
            Security = "tls"
        };
    }

    private static ServerProfile ParseHysteria2Uri(string payload, bool legacy = false)
    {
        var link = $"hysteria2://{payload}";
        var uri = new Uri(link);
        var query = ParseQuery(uri.Query);
        var password = Uri.UnescapeDataString(uri.UserInfo);
        if (string.IsNullOrWhiteSpace(password) && query.TryGetValue("auth", out var auth))
        {
            password = auth;
        }

        return new ServerProfile
        {
            Protocol = legacy ? VpnProtocol.Hysteria : VpnProtocol.Hysteria2,
            Name = string.IsNullOrWhiteSpace(uri.Fragment.TrimStart('#'))
                ? (legacy ? "Hysteria" : "Hysteria2")
                : Uri.UnescapeDataString(uri.Fragment.TrimStart('#')),
            Address = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 443,
            Password = password,
            UserId = password,
            Sni = query.GetValueOrDefault("sni", query.GetValueOrDefault("peer", "")),
            TransportMode = query.GetValueOrDefault("upmbps", query.GetValueOrDefault("up", "")),
            Alpn = query.GetValueOrDefault("downmbps", query.GetValueOrDefault("down", "")),
            Security = "tls"
        };
    }

    private static string TryDecodeBase64Field(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(SubscriptionContentDecoder.PadBase64(value)));
        }
        catch
        {
            return Uri.UnescapeDataString(value);
        }
    }

    private static string BuildVmessLink(ServerProfile s)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["v"] = "2", ["ps"] = s.Name, ["add"] = s.Address, ["port"] = s.Port.ToString(),
            ["id"] = s.UserId, ["scy"] = s.Encryption, ["net"] = s.Network,
            ["host"] = s.Host, ["path"] = s.Path, ["tls"] = s.Security == "tls" ? "tls" : "",
            ["sni"] = s.Sni
        });
        return "vmess://" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static string BuildVlessLink(ServerProfile s) =>
        $"vless://{s.UserId}@{s.Address}:{s.Port}?encryption=none&security={s.Security}&type={s.Network}#{Uri.EscapeDataString(s.Name)}";

    private static string BuildTrojanLink(ServerProfile s) =>
        $"trojan://{Uri.EscapeDataString(s.Password)}@{s.Address}:{s.Port}#{Uri.EscapeDataString(s.Name)}";

    private static string BuildSsLink(ServerProfile s)
    {
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.Encryption}:{s.Password}"));
        return $"ss://{creds}@{s.Address}:{s.Port}#{Uri.EscapeDataString(s.Name)}";
    }

    private static string BuildHy2Link(ServerProfile s) =>
        $"hysteria2://{Uri.EscapeDataString(s.Password)}@{s.Address}:{s.Port}?sni={Uri.EscapeDataString(s.Sni)}#{Uri.EscapeDataString(s.Name)}";

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(query.TrimStart('?'), "([^=&]+)=([^&]*)"))
        {
            result[Uri.UnescapeDataString(match.Groups[1].Value)] = Uri.UnescapeDataString(match.Groups[2].Value);
        }

        return result;
    }

    private static int ParsePort(JsonElement port) =>
        port.ValueKind == JsonValueKind.String ? int.Parse(port.GetString()!) : port.GetInt32();
}
