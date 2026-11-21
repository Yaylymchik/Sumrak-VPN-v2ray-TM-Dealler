using System.Text.Json.Nodes;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// sing-box outbounds for TUN mode (all common proxy protocols through one core).
/// </summary>
public static class SingBoxUniversalOutboundBuilder
{
    public static bool TryBuild(ServerProfile s, out JsonObject outbound)
    {
        outbound = s.Protocol switch
        {
            VpnProtocol.VLESS => BuildVless(s),
            VpnProtocol.VMess => BuildVmess(s),
            VpnProtocol.Trojan => BuildTrojan(s),
            VpnProtocol.Shadowsocks => BuildShadowsocks(s),
            VpnProtocol.Hysteria2 => BuildHysteria2(s),
            VpnProtocol.Hysteria => BuildHysteria(s),
            VpnProtocol.Tuic => BuildTuic(s),
            VpnProtocol.ShadowsocksR => BuildSsr(s),
            VpnProtocol.Socks => BuildSocks(s),
            _ => null!
        };

        return outbound != null;
    }

    private static JsonObject BuildVless(ServerProfile s)
    {
        var o = new JsonObject
        {
            ["type"] = "vless",
            ["tag"] = "proxy",
            ["server"] = s.Address,
            ["server_port"] = s.Port,
            ["uuid"] = s.UserId
        };
        if (!string.IsNullOrWhiteSpace(s.Flow))
        {
            o["flow"] = s.Flow;
        }

        ApplyTls(o, s);
        ApplyTransport(o, s);
        return o;
    }

    private static JsonObject BuildVmess(ServerProfile s)
    {
        var o = new JsonObject
        {
            ["type"] = "vmess",
            ["tag"] = "proxy",
            ["server"] = s.Address,
            ["server_port"] = s.Port,
            ["uuid"] = s.UserId,
            ["security"] = string.IsNullOrWhiteSpace(s.Encryption) ? "auto" : s.Encryption,
            ["alter_id"] = 0
        };
        ApplyTls(o, s);
        ApplyTransport(o, s);
        return o;
    }

    private static JsonObject BuildTrojan(ServerProfile s) => new()
    {
        ["type"] = "trojan",
        ["tag"] = "proxy",
        ["server"] = s.Address,
        ["server_port"] = s.Port,
        ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password,
        ["tls"] = BuildTlsObject(s)
    };

    private static JsonObject BuildShadowsocks(ServerProfile s) => new()
    {
        ["type"] = "shadowsocks",
        ["tag"] = "proxy",
        ["server"] = s.Address,
        ["server_port"] = s.Port,
        ["method"] = string.IsNullOrWhiteSpace(s.Encryption) ? "aes-256-gcm" : s.Encryption,
        ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password
    };

    private static JsonObject BuildHysteria2(ServerProfile s) => new()
    {
        ["type"] = "hysteria2",
        ["tag"] = "proxy",
        ["server"] = s.Address,
        ["server_port"] = s.Port,
        ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password,
        ["tls"] = BuildTlsObject(s)
    };

    private static JsonObject BuildHysteria(ServerProfile s) => SingBoxConfigBuilder.BuildHysteriaPublic(s);

    private static JsonObject BuildTuic(ServerProfile s)
    {
        var o = new JsonObject
        {
            ["type"] = "tuic",
            ["tag"] = "proxy",
            ["server"] = s.Address,
            ["server_port"] = s.Port,
            ["uuid"] = s.UserId,
            ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password,
            ["congestion_control"] = string.IsNullOrWhiteSpace(s.TransportMode) ? "bbr" : s.TransportMode,
            ["udp_relay_mode"] = "native",
            ["tls"] = BuildTlsObject(s)
        };
        return o;
    }

    private static JsonObject BuildSsr(ServerProfile s) => SingBoxConfigBuilder.BuildSsrPublic(s);

    private static JsonObject BuildSocks(ServerProfile s)
    {
        var o = new JsonObject
        {
            ["type"] = "socks",
            ["tag"] = "proxy",
            ["server"] = s.Address,
            ["server_port"] = s.Port,
            ["version"] = "5"
        };
        if (!string.IsNullOrWhiteSpace(s.UserId) || !string.IsNullOrWhiteSpace(s.Password))
        {
            o["username"] = s.UserId;
            o["password"] = s.Password;
        }

        return o;
    }

    private static void ApplyTls(JsonObject o, ServerProfile s)
    {
        if (s.Security is "tls" or "reality")
        {
            o["tls"] = BuildTlsObject(s);
        }
    }

    private static JsonObject BuildTlsObject(ServerProfile s)
    {
        var tls = new JsonObject { ["enabled"] = true };
        var sni = string.IsNullOrWhiteSpace(s.Sni) ? s.Host : s.Sni;
        if (!string.IsNullOrWhiteSpace(sni))
        {
            tls["server_name"] = sni;
        }

        if (!string.IsNullOrWhiteSpace(s.Alpn))
        {
            var alpn = new JsonArray();
            foreach (var item in s.Alpn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                alpn.Add(item);
            }

            tls["alpn"] = alpn;
        }

        if (s.Security == "reality")
        {
            tls["reality"] = new JsonObject { ["enabled"] = true };
        }

        return tls;
    }

    private static void ApplyTransport(JsonObject o, ServerProfile s)
    {
        var net = (s.Network ?? "tcp").ToLowerInvariant();
        if (net is "" or "tcp" or "raw")
        {
            return;
        }

        var transport = new JsonObject { ["type"] = net == "httpupgrade" ? "ws" : net };
        if (net is "ws" or "httpupgrade")
        {
            transport["path"] = string.IsNullOrWhiteSpace(s.Path) ? "/" : s.Path;
            if (!string.IsNullOrWhiteSpace(s.Host))
            {
                transport["headers"] = new JsonObject { ["Host"] = s.Host };
            }
        }
        else if (net == "grpc")
        {
            transport["service_name"] = s.Path ?? "";
        }
        else if (net == "xhttp")
        {
            transport["path"] = string.IsNullOrWhiteSpace(s.Path) ? "/" : s.Path;
        }

        o["transport"] = transport;
    }
}
