using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// sing-box client config — proxy inbounds and optional TUN full-tunnel mode.
/// </summary>
public static class SingBoxConfigBuilder
{
    private static readonly JsonSerializerOptions WriteOptions = JsonCompat.CreateIndentedWriteOptions();

    private static void EnsureDistinctProxyPorts(AppSettings settings)
    {
        if (settings.LocalSocksPort < 1024 || settings.LocalSocksPort > 65535)
        {
            settings.LocalSocksPort = 10808;
        }

        if (settings.LocalHttpPort < 1024 || settings.LocalHttpPort > 65535 ||
            settings.LocalHttpPort == settings.LocalSocksPort)
        {
            settings.LocalHttpPort = settings.LocalSocksPort >= 65535
                ? settings.LocalSocksPort - 1
                : settings.LocalSocksPort + 1;
        }
    }

    public static string Build(ServerProfile server, AppSettings settings, bool forceProxyMode = false)
    {
        EnsureDistinctProxyPorts(settings);
        var useTun = !forceProxyMode && settings.UseTunMode;
        JsonObject outbound;
        if (useTun && SingBoxUniversalOutboundBuilder.TryBuild(server, out var universal))
        {
            outbound = universal;
        }
        else
        {
            outbound = BuildOutbound(server);
        }

        outbound["domain_resolver"] = "dns-remote";

        var inbounds = new JsonArray();
        if (useTun)
        {
            inbounds.Add(BuildTunInbound(settings, server));
        }

        inbounds.Add(new JsonObject
        {
            ["type"] = "mixed",
            ["tag"] = "mixed-in",
            ["listen"] = ProxyListenHelper.GetListenAddress(settings),
            ["listen_port"] = settings.LocalHttpPort
        });
        inbounds.Add(new JsonObject
        {
            ["type"] = "socks",
            ["tag"] = "socks-in",
            ["listen"] = ProxyListenHelper.GetListenAddress(settings),
            ["listen_port"] = settings.LocalSocksPort
        });

        var rules = new JsonArray();
        if (settings.SniffTraffic)
        {
            var sniffInbounds = new JsonArray();
            if (useTun)
            {
                sniffInbounds.Add("tun-in");
            }

            sniffInbounds.Add("mixed-in");
            sniffInbounds.Add("socks-in");
            rules.Add(new JsonObject
            {
                ["inbound"] = sniffInbounds,
                ["action"] = "sniff",
                ["timeout"] = "1s"
            });
        }

        RoutingRulesBuilder.ApplySingBoxRules(rules, settings, useTun, server);

        var root = new JsonObject
        {
            ["log"] = new JsonObject
            {
                ["level"] = useTun ? "info" : "warn",
                ["timestamp"] = true,
                ["output"] = Path.Combine(AppPaths.LogsRoot, "sing-box.log")
            },
            ["dns"] = SingBoxDnsBuilder.Build(settings),
            ["inbounds"] = inbounds,
            ["outbounds"] = new JsonArray
            {
                outbound,
                new JsonObject { ["type"] = "direct", ["tag"] = "direct" },
                new JsonObject { ["type"] = "block", ["tag"] = "block" }
            },
            ["route"] = new JsonObject
            {
                ["auto_detect_interface"] = true,
                ["final"] = "proxy",
                ["default_domain_resolver"] = "dns-remote",
                ["rules"] = rules
            }
        };

        return root.ToJsonString(WriteOptions);
    }

    private static JsonObject BuildTunInbound(AppSettings settings, ServerProfile server)
    {
        var routeExclude = new JsonArray
        {
            "192.168.0.0/16",
            "10.0.0.0/8",
            "172.16.0.0/12",
            "127.0.0.0/8"
        };

        if (IPAddress.TryParse(server.Address, out _))
        {
            routeExclude.Add($"{server.Address}/32");
        }

        return new JsonObject
        {
            ["type"] = "tun",
            ["tag"] = "tun-in",
            ["interface_name"] = "SumraX",
            ["address"] = new JsonArray { "172.19.0.1/30" },
            ["mtu"] = settings.TunMtu,
            ["auto_route"] = true,
            ["strict_route"] = true,
            ["stack"] = "system",
            ["endpoint_independent_nat"] = true,
            ["route_address"] = new JsonArray { "0.0.0.0/1", "128.0.0.0/1" },
            ["route_exclude_address"] = routeExclude
        };
    }

    private static JsonObject BuildOutbound(ServerProfile server) => server.Protocol switch
    {
        VpnProtocol.Tuic => BuildTuic(server),
        VpnProtocol.ShadowsocksR => BuildSsr(server),
        VpnProtocol.Hysteria => BuildHysteria(server),
        _ => throw new NotSupportedException($"sing-box does not handle {server.Protocol}.")
    };

    internal static JsonObject BuildHysteriaPublic(ServerProfile s) => BuildHysteria(s);

    internal static JsonObject BuildSsrPublic(ServerProfile s) => BuildSsr(s);

    private static JsonObject BuildTuic(ServerProfile s)
    {
        var tls = new JsonObject { ["enabled"] = true };
        if (!string.IsNullOrWhiteSpace(s.Sni))
        {
            tls["server_name"] = s.Sni;
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

        return new JsonObject
        {
            ["type"] = "tuic",
            ["tag"] = "proxy",
            ["server"] = s.Address,
            ["server_port"] = s.Port,
            ["uuid"] = s.UserId,
            ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password,
            ["congestion_control"] = string.IsNullOrWhiteSpace(s.TransportMode) ? "bbr" : s.TransportMode,
            ["udp_relay_mode"] = "native",
            ["zero_rtt_handshake"] = false,
            ["heartbeat"] = "10s",
            ["tls"] = tls
        };
    }

    private static JsonObject BuildHysteria(ServerProfile s)
    {
        var tls = new JsonObject { ["enabled"] = true };
        if (!string.IsNullOrWhiteSpace(s.Sni))
        {
            tls["server_name"] = s.Sni;
        }

        var outbound = new JsonObject
        {
            ["type"] = "hysteria",
            ["tag"] = "proxy",
            ["server"] = s.Address,
            ["server_port"] = s.Port,
            ["auth_str"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password,
            ["tls"] = tls
        };

        if (int.TryParse(s.TransportMode, out var up) && up > 0)
        {
            outbound["up_mbps"] = up;
        }

        if (int.TryParse(s.Alpn, out var down) && down > 0)
        {
            outbound["down_mbps"] = down;
        }

        return outbound;
    }

    private static JsonObject BuildSsr(ServerProfile s)
    {
        var extra = SsrExtra.Parse(s.Extra);
        var obfs = extra.Obfs?.ToLowerInvariant() ?? "plain";
        var ssrProtocol = extra.SsrProtocol?.ToLowerInvariant() ?? "origin";

        if (!ssrProtocol.Equals("origin", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException($"SSR protocol '{ssrProtocol}' is not supported. Use origin/plain or obfs tls/http/simple.");
        }

        var outbound = new JsonObject
        {
            ["type"] = "shadowsocks",
            ["tag"] = "proxy",
            ["server"] = s.Address,
            ["server_port"] = s.Port,
            ["method"] = string.IsNullOrWhiteSpace(s.Encryption) ? "aes-256-cfb" : s.Encryption,
            ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password
        };

        if (obfs is "tls" or "http" or "simple")
        {
            var mode = obfs == "simple" ? "http" : obfs;
            var host = string.IsNullOrWhiteSpace(extra.ObfsParam) ? "www.bing.com" : extra.ObfsParam;
            outbound["plugin"] = "obfs-local";
            outbound["plugin_opts"] = $"obfs={mode};obfs-host={host}";
        }

        return outbound;
    }

    internal sealed class SsrExtra
    {
        public string? SsrProtocol { get; init; }
        public string? Obfs { get; init; }
        public string? ObfsParam { get; init; }
        public string? ProtocolParam { get; init; }

        public static SsrExtra Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new SsrExtra();
            }

            try
            {
                return JsonSerializer.Deserialize<SsrExtra>(json) ?? new SsrExtra();
            }
            catch
            {
                return new SsrExtra();
            }
        }

        public static string Serialize(string ssrProtocol, string obfs, string? obfsParam, string? protocolParam) =>
            JsonSerializer.Serialize(new SsrExtra
            {
                SsrProtocol = ssrProtocol,
                Obfs = obfs,
                ObfsParam = obfsParam,
                ProtocolParam = protocolParam
            });
    }
}
