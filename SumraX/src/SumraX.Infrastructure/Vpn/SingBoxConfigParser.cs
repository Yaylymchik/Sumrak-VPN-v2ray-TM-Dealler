using System.Text.Json;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Parses sing-box / Xray JSON outbounds into ServerProfile.
/// </summary>
public static class SingBoxConfigParser
{
    public static ServerProfile? TryParseOutbound(JsonElement outbound)
    {
        // sing-box style: "type": "vless"
        if (outbound.TryGetProperty("type", out var singType))
        {
            return ParseSingBoxOutbound(singType.GetString()?.ToLowerInvariant(), outbound);
        }

        // xray style: "protocol": "vless"
        if (outbound.TryGetProperty("protocol", out var xrayProtocol))
        {
            return ParseXrayOutbound(xrayProtocol.GetString()?.ToLowerInvariant(), outbound);
        }

        return null;
    }

    private static ServerProfile? ParseSingBoxOutbound(string? type, JsonElement el) => type switch
    {
        "vless" => new ServerProfile
        {
            Protocol = VpnProtocol.VLESS,
            Name = GetTag(el, "VLESS"),
            Address = el.GetProperty("server").GetString() ?? "",
            Port = el.TryGetProperty("server_port", out var p) ? p.GetInt32() : 443,
            UserId = el.GetProperty("uuid").GetString() ?? "",
            Flow = el.TryGetProperty("flow", out var flow) ? flow.GetString() ?? "" : "",
            Network = GetTransport(el),
            Security = el.TryGetProperty("tls", out _) ? "tls" : "none"
        },
        "vmess" => new ServerProfile
        {
            Protocol = VpnProtocol.VMess,
            Name = GetTag(el, "VMess"),
            Address = el.GetProperty("server").GetString() ?? "",
            Port = el.TryGetProperty("server_port", out var p) ? p.GetInt32() : 443,
            UserId = el.GetProperty("uuid").GetString() ?? "",
            Encryption = el.TryGetProperty("security", out var sec) ? sec.GetString() ?? "auto" : "auto",
            Network = GetTransport(el),
            Security = el.TryGetProperty("tls", out _) ? "tls" : "none"
        },
        "trojan" => new ServerProfile
        {
            Protocol = VpnProtocol.Trojan,
            Name = GetTag(el, "Trojan"),
            Address = el.GetProperty("server").GetString() ?? "",
            Port = el.TryGetProperty("server_port", out var p) ? p.GetInt32() : 443,
            Password = el.GetProperty("password").GetString() ?? "",
            UserId = el.GetProperty("password").GetString() ?? "",
            Security = el.TryGetProperty("tls", out _) ? "tls" : "none"
        },
        "shadowsocks" => new ServerProfile
        {
            Protocol = VpnProtocol.Shadowsocks,
            Name = GetTag(el, "Shadowsocks"),
            Address = el.GetProperty("server").GetString() ?? "",
            Port = el.TryGetProperty("server_port", out var p) ? p.GetInt32() : 8388,
            Encryption = el.GetProperty("method").GetString() ?? "aes-256-gcm",
            Password = el.GetProperty("password").GetString() ?? "",
            UserId = el.GetProperty("password").GetString() ?? ""
        },
        "hysteria" => new ServerProfile
        {
            Protocol = VpnProtocol.Hysteria,
            Name = GetTag(el, "Hysteria"),
            Address = el.GetProperty("server").GetString() ?? "",
            Port = el.TryGetProperty("server_port", out var hp) ? hp.GetInt32() : 443,
            Password = el.TryGetProperty("auth_str", out var auth) ? auth.GetString() ?? "" : "",
            UserId = el.TryGetProperty("auth_str", out var auth2) ? auth2.GetString() ?? "" : "",
            Security = "tls"
        },
        "hysteria2" => new ServerProfile
        {
            Protocol = VpnProtocol.Hysteria2,
            Name = GetTag(el, "Hysteria2"),
            Address = el.GetProperty("server").GetString() ?? "",
            Port = el.TryGetProperty("server_port", out var hp) ? hp.GetInt32() : 443,
            Password = el.GetProperty("password").GetString() ?? "",
            UserId = el.GetProperty("password").GetString() ?? "",
            Security = "tls"
        },
        "tuic" => new ServerProfile
        {
            Protocol = VpnProtocol.Tuic,
            Name = GetTag(el, "TUIC"),
            Address = el.GetProperty("server").GetString() ?? "",
            Port = el.TryGetProperty("server_port", out var tp) ? tp.GetInt32() : 443,
            UserId = el.GetProperty("uuid").GetString() ?? "",
            Password = el.TryGetProperty("password", out var pw) ? pw.GetString() ?? "" : "",
            TransportMode = el.TryGetProperty("congestion_control", out var cc) ? cc.GetString() ?? "bbr" : "bbr",
            Security = "tls"
        },
        _ => null
    };

    private static ServerProfile? ParseXrayOutbound(string? protocol, JsonElement outbound) => protocol switch
    {
        "vmess" => ParseXrayVmess(outbound),
        "vless" => ParseXrayVless(outbound),
        "trojan" => ParseXrayTrojan(outbound),
        "shadowsocks" => ParseXraySs(outbound),
        "hysteria2" => ParseXrayHysteria2(outbound),
        "socks" => ParseXraySocks(outbound),
        "wireguard" => ParseXrayWireGuard(outbound),
        _ => null
    };

    private static ServerProfile? ParseXrayVmess(JsonElement outbound)
    {
        if (!outbound.TryGetProperty("settings", out var settings) ||
            !settings.TryGetProperty("vnext", out var vnext) ||
            vnext.GetArrayLength() == 0)
        {
            return null;
        }

        var node = vnext[0];
        var user = node.GetProperty("users")[0];
        var profile = new ServerProfile
        {
            Protocol = VpnProtocol.VMess,
            Name = outbound.TryGetProperty("tag", out var tag) ? tag.GetString() ?? "VMess" : "VMess",
            Address = node.GetProperty("address").GetString() ?? "",
            Port = node.GetProperty("port").GetInt32(),
            UserId = user.GetProperty("id").GetString() ?? "",
            Encryption = user.TryGetProperty("security", out var sec) ? sec.GetString() ?? "auto" : "auto"
        };
        ApplyXrayStream(outbound, profile);
        return profile;
    }

    private static ServerProfile? ParseXrayVless(JsonElement outbound)
    {
        if (!outbound.TryGetProperty("settings", out var settings) ||
            !settings.TryGetProperty("vnext", out var vnext) ||
            vnext.GetArrayLength() == 0)
        {
            return null;
        }

        var node = vnext[0];
        var user = node.GetProperty("users")[0];
        var profile = new ServerProfile
        {
            Protocol = VpnProtocol.VLESS,
            Name = outbound.TryGetProperty("tag", out var tag) ? tag.GetString() ?? "VLESS" : "VLESS",
            Address = node.GetProperty("address").GetString() ?? "",
            Port = node.GetProperty("port").GetInt32(),
            UserId = user.GetProperty("id").GetString() ?? "",
            Flow = user.TryGetProperty("flow", out var flow) ? flow.GetString() ?? "" : ""
        };
        ApplyXrayStream(outbound, profile);
        return profile;
    }

    private static ServerProfile? ParseXrayTrojan(JsonElement outbound)
    {
        if (!outbound.TryGetProperty("settings", out var settings) ||
            !settings.TryGetProperty("servers", out var servers) ||
            servers.GetArrayLength() == 0)
        {
            return null;
        }

        var server = servers[0];
        var profile = new ServerProfile
        {
            Protocol = VpnProtocol.Trojan,
            Name = outbound.TryGetProperty("tag", out var tag) ? tag.GetString() ?? "Trojan" : "Trojan",
            Address = server.GetProperty("address").GetString() ?? "",
            Port = server.GetProperty("port").GetInt32(),
            Password = server.GetProperty("password").GetString() ?? "",
            UserId = server.GetProperty("password").GetString() ?? ""
        };
        ApplyXrayStream(outbound, profile);
        return profile;
    }

    private static ServerProfile? ParseXraySs(JsonElement outbound)
    {
        if (!outbound.TryGetProperty("settings", out var settings) ||
            !settings.TryGetProperty("servers", out var servers) ||
            servers.GetArrayLength() == 0)
        {
            return null;
        }

        var server = servers[0];
        return new ServerProfile
        {
            Protocol = VpnProtocol.Shadowsocks,
            Name = outbound.TryGetProperty("tag", out var tag) ? tag.GetString() ?? "SS" : "SS",
            Address = server.GetProperty("address").GetString() ?? "",
            Port = server.GetProperty("port").GetInt32(),
            Encryption = server.GetProperty("method").GetString() ?? "aes-256-gcm",
            Password = server.GetProperty("password").GetString() ?? "",
            UserId = server.GetProperty("password").GetString() ?? ""
        };
    }

    private static ServerProfile? ParseXrayHysteria2(JsonElement outbound)
    {
        if (!outbound.TryGetProperty("settings", out var settings) ||
            !settings.TryGetProperty("servers", out var servers) ||
            servers.GetArrayLength() == 0)
        {
            return null;
        }

        var server = servers[0];
        return new ServerProfile
        {
            Protocol = VpnProtocol.Hysteria2,
            Name = outbound.TryGetProperty("tag", out var tag) ? tag.GetString() ?? "Hysteria2" : "Hysteria2",
            Address = server.GetProperty("address").GetString() ?? "",
            Port = server.GetProperty("port").GetInt32(),
            Password = server.GetProperty("password").GetString() ?? "",
            UserId = server.GetProperty("password").GetString() ?? "",
            Sni = server.TryGetProperty("sni", out var sni) ? sni.GetString() ?? "" : "",
            Security = "tls"
        };
    }

    private static void ApplyXrayStream(JsonElement outbound, ServerProfile profile)
    {
        if (!outbound.TryGetProperty("streamSettings", out var stream))
        {
            return;
        }

        profile.Network = stream.TryGetProperty("network", out var net) ? net.GetString() ?? "tcp" : "tcp";
        profile.Security = stream.TryGetProperty("security", out var sec) ? sec.GetString() ?? "none" : "none";
        if (stream.TryGetProperty("wsSettings", out var ws))
        {
            profile.Path = ws.TryGetProperty("path", out var path) ? path.GetString() ?? "" : "";
            if (ws.TryGetProperty("headers", out var headers) && headers.TryGetProperty("Host", out var host))
            {
                profile.Host = host.GetString() ?? "";
            }
        }

        if (stream.TryGetProperty("tlsSettings", out var tls) && tls.TryGetProperty("serverName", out var sni))
        {
            profile.Sni = sni.GetString() ?? "";
        }
    }

    private static ServerProfile? ParseXraySocks(JsonElement outbound)
    {
        if (!outbound.TryGetProperty("settings", out var settings) ||
            !settings.TryGetProperty("servers", out var servers) ||
            servers.GetArrayLength() == 0)
        {
            return null;
        }

        var server = servers[0];
        var profile = new ServerProfile
        {
            Protocol = VpnProtocol.Socks,
            Name = outbound.TryGetProperty("tag", out var tag) ? tag.GetString() ?? "SOCKS" : "SOCKS",
            Address = server.GetProperty("address").GetString() ?? "",
            Port = server.GetProperty("port").GetInt32()
        };

        if (server.TryGetProperty("users", out var users) && users.GetArrayLength() > 0)
        {
            var user = users[0];
            profile.UserId = user.TryGetProperty("user", out var u) ? u.GetString() ?? "" : "";
            profile.Password = user.TryGetProperty("pass", out var p) ? p.GetString() ?? "" : "";
        }

        return profile;
    }

    private static ServerProfile? ParseXrayWireGuard(JsonElement outbound)
    {
        if (!outbound.TryGetProperty("settings", out var settings))
        {
            return null;
        }

        var secret = settings.TryGetProperty("secretKey", out var sk) ? sk.GetString() ?? "" : "";
        var peers = settings.TryGetProperty("peers", out var p) && p.GetArrayLength() > 0 ? p[0] : default;
        var endpoint = peers.ValueKind != JsonValueKind.Undefined && peers.TryGetProperty("endpoint", out var ep)
            ? ep.GetString() ?? ""
            : "";
        var host = endpoint;
        var port = 51820;
        if (endpoint.Contains(':'))
        {
            var idx = endpoint.LastIndexOf(':');
            host = endpoint[..idx];
            int.TryParse(endpoint[(idx + 1)..], out port);
        }

        var extra = XrayConfigBuilder.WireGuardExtra.Serialize(new XrayConfigBuilder.WireGuardExtra
        {
            SecretKey = secret,
            PublicKey = peers.ValueKind != JsonValueKind.Undefined && peers.TryGetProperty("publicKey", out var pk)
                ? pk.GetString() ?? "" : "",
            Endpoint = endpoint
        });

        return new ServerProfile
        {
            Protocol = VpnProtocol.WireGuard,
            Name = outbound.TryGetProperty("tag", out var tag) ? tag.GetString() ?? "WireGuard" : "WireGuard",
            Address = host,
            Port = port,
            Extra = extra
        };
    }

    private static string GetTransport(JsonElement el)
    {
        foreach (var key in new[] { "transport", "network" })
        {
            if (el.TryGetProperty(key, out var t))
            {
                return t.GetString() ?? "tcp";
            }
        }

        if (el.TryGetProperty("ws", out _)) return "ws";
        if (el.TryGetProperty("grpc", out _)) return "grpc";
        return "tcp";
    }

    private static string GetTag(JsonElement el, string fallback) =>
        el.TryGetProperty("tag", out var tag) ? tag.GetString() ?? fallback : fallback;
}
