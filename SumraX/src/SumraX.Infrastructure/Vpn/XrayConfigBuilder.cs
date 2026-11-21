using System.Text.Json;
using System.Text.Json.Nodes;
using SumraX.Core;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Generates Xray-core compatible client configuration JSON.
/// </summary>
public static class XrayConfigBuilder
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
        var useTun = CoreSelector.UseTunMode(settings, forceProxyMode);
        var outbound = BuildOutbound(server, settings);
        var inbounds = new JsonArray();

        if (useTun)
        {
            var dnsServer = string.IsNullOrWhiteSpace(settings.DnsServer) ? "1.1.1.1" : settings.DnsServer.Trim();
            var tun = new JsonObject
            {
                ["tag"] = "tun-in",
                ["protocol"] = "tun",
                ["settings"] = new JsonObject
                {
                    ["name"] = "SumraX",
                    ["mtu"] = settings.TunMtu,
                    ["gateway"] = new JsonArray { "172.19.0.1/30" },
                    ["dns"] = new JsonArray { dnsServer, "8.8.8.8" },
                    ["autoSystemRoutingTable"] = new JsonArray { "0.0.0.0/1", "128.0.0.0/1" },
                    ["autoOutboundsInterface"] = "auto"
                }
            };

            if (settings.SniffTraffic)
            {
                var sniffing = new JsonObject
                {
                    ["enabled"] = true,
                    ["destOverride"] = new JsonArray { "http", "tls", "quic" }
                };
                if (settings.RouteOnly)
                {
                    sniffing["routeOnly"] = true;
                }

                tun["sniffing"] = sniffing;
            }

            inbounds.Add(tun);
        }

        inbounds.Add(BuildSocksInbound(settings, useTun));
        inbounds.Add(new JsonObject
        {
            ["tag"] = "http-in",
            ["port"] = settings.LocalHttpPort,
            ["listen"] = ProxyListenHelper.GetListenAddress(settings),
            ["protocol"] = "http"
        });

        var rules = new JsonArray();
        RoutingRulesBuilder.ApplyXrayRules(rules, settings, useTun, server);

        ApplyMux(outbound, settings);
        ApplyFragment(outbound, settings);

        var outbounds = new JsonArray { outbound };
        if (settings.FragmentEnabled && NeedsFragmentDialer(server))
        {
            outbounds.Add(BuildFragmentOutbound(settings));
        }

        outbounds.Add(BuildDirectOutbound(settings));
        outbounds.Add(BuildBlockOutbound());

        var root = new JsonObject
        {
            ["log"] = new JsonObject
            {
                ["loglevel"] = string.IsNullOrWhiteSpace(settings.CoreLogLevel) ? "warning" : settings.CoreLogLevel.Trim()
            },
            ["inbounds"] = inbounds,
            ["outbounds"] = outbounds,
            ["routing"] = new JsonObject
            {
                ["domainStrategy"] = settings.EnableServerResolving ? "IPIfNonMatch" : "AsIs",
                ["rules"] = rules
            }
        };

        if (BuildDnsSection(settings) is { } dns)
        {
            root["dns"] = dns;
        }

        if (settings.FakeDnsEnabled && settings.UseLocalDns)
        {
            root["fakedns"] = new JsonArray
            {
                new JsonObject
                {
                    ["ipPool"] = "198.18.0.0/15",
                    ["poolSize"] = 65535
                }
            };
        }

        return root.ToJsonString(WriteOptions);
    }

    /// <summary>
    /// Lightweight config for real ping (v2rayNG getV2rayConfig4Speedtest): SOCKS-only, no TUN/DNS/mux.
    /// </summary>
    public static string BuildForSpeedtest(ServerProfile server, AppSettings settings, int socksPort)
    {
        var speedSettings = CloneForSpeedtest(settings, socksPort);
        var outbound = BuildOutbound(server, speedSettings);
        // Real ping: no mux (matches Android postProcessForSpeedtest)
        ApplyFragment(outbound, speedSettings);

        var outbounds = new JsonArray { outbound };
        if (speedSettings.FragmentEnabled && NeedsFragmentDialer(server))
        {
            outbounds.Add(BuildFragmentOutbound(speedSettings));
        }

        outbounds.Add(BuildDirectOutbound(speedSettings));
        outbounds.Add(BuildBlockOutbound());

        var root = new JsonObject
        {
            ["log"] = new JsonObject { ["loglevel"] = "error" },
            ["inbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["tag"] = "socks-in",
                    ["port"] = socksPort,
                    ["listen"] = "127.0.0.1",
                    ["protocol"] = "socks",
                    ["settings"] = new JsonObject { ["udp"] = false }
                },
                new JsonObject
                {
                    ["tag"] = "http-in",
                    ["port"] = socksPort + 1,
                    ["listen"] = "127.0.0.1",
                    ["protocol"] = "http"
                }
            },
            ["outbounds"] = outbounds,
            ["routing"] = new JsonObject
            {
                ["domainStrategy"] = "AsIs",
                ["rules"] = new JsonArray()
            }
        };

        return root.ToJsonString(WriteOptions);
    }

    private static AppSettings CloneForSpeedtest(AppSettings settings, int socksPort) => new()
    {
        LocalSocksPort = socksPort,
        LocalHttpPort = socksPort + 1,
        AllowLanConnections = false,
        UseTunMode = false,
        SniffTraffic = false,
        RouteOnly = false,
        MuxEnabled = false,
        FragmentEnabled = settings.FragmentEnabled,
        FragmentPackets = settings.FragmentPackets,
        FragmentLength = settings.FragmentLength,
        FragmentInterval = settings.FragmentInterval,
        FragmentMaxSplit = settings.FragmentMaxSplit,
        NoiseEnabled = settings.NoiseEnabled,
        NoiseRand = settings.NoiseRand,
        NoiseDelay = settings.NoiseDelay,
        SubscriptionUserAgent = settings.SubscriptionUserAgent,
        CoreLogLevel = "error",
        Ipv6Enabled = settings.Ipv6Enabled,
        PreferIpv6 = settings.PreferIpv6
    };

    private static JsonObject BuildSocksInbound(AppSettings settings, bool useTun)
    {
        var socks = new JsonObject
        {
            ["tag"] = "socks-in",
            ["port"] = settings.LocalSocksPort,
            ["listen"] = ProxyListenHelper.GetListenAddress(settings),
            ["protocol"] = "socks",
            ["settings"] = new JsonObject { ["udp"] = settings.SocksEnableUdp }
        };

        if (settings.SniffTraffic && !useTun)
        {
            var sniffing = new JsonObject
            {
                ["enabled"] = true,
                ["destOverride"] = new JsonArray { "http", "tls", "quic" }
            };
            if (settings.RouteOnly)
            {
                sniffing["routeOnly"] = true;
            }

            socks["sniffing"] = sniffing;
        }

        return socks;
    }

    private static string DnsQueryStrategy(AppSettings settings)
    {
        if (settings.PreferIpv6)
        {
            return "UseIPv6";
        }

        return settings.Ipv6Enabled ? "UseIP" : "UseIPv4";
    }

    private static JsonObject? BuildDnsSection(AppSettings settings)
    {
        if (!settings.UseDnsFromJson)
        {
            if (!settings.UseLocalDns)
            {
                return null;
            }

            return new JsonObject
            {
                ["servers"] = new JsonArray { "localhost" },
                ["queryStrategy"] = DnsQueryStrategy(settings)
            };
        }

        var servers = new JsonArray();
        if (settings.UseLocalDns)
        {
            servers.Add("localhost");
        }

        if (settings.FakeDnsEnabled && settings.UseLocalDns)
        {
            servers.Add("fakedns");
        }

        servers.Add(settings.RemoteDns);
        servers.Add(settings.DnsServer);
        if (!string.IsNullOrWhiteSpace(settings.DomesticDns))
        {
            servers.Add(settings.DomesticDns.Trim());
        }

        var dns = new JsonObject
        {
            ["servers"] = servers,
            ["queryStrategy"] = DnsQueryStrategy(settings)
        };

        if (!string.IsNullOrWhiteSpace(settings.DnsHosts))
        {
            var hosts = new JsonObject();
            foreach (var line in settings.DnsHosts.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split(['=', ' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    hosts[parts[0]] = parts[1];
                }
            }

            if (hosts.Count > 0)
            {
                dns["hosts"] = hosts;
            }
        }

        return dns;
    }

    private static void ApplyMux(JsonObject outbound, AppSettings settings)
    {
        if (!settings.MuxEnabled)
        {
            return;
        }

        outbound["mux"] = new JsonObject
        {
            ["enabled"] = true,
            ["concurrency"] = Math.Clamp(settings.MuxConcurrency, 1, 1024),
            ["xudpConcurrency"] = Math.Clamp(settings.MuxXudpConcurrency, 1, 1024),
            ["xudpProxyUDP443"] = string.IsNullOrWhiteSpace(settings.MuxXudpQuic) ? "reject" : settings.MuxXudpQuic
        };
    }

    private static bool NeedsFragmentDialer(ServerProfile server) =>
        server.Security is "tls" or "reality";

    private static void ApplyFragment(JsonObject outbound, AppSettings settings)
    {
        if (!settings.FragmentEnabled || outbound["streamSettings"] is not JsonObject stream)
        {
            return;
        }

        if (stream["security"]?.GetValue<string>() is not ("tls" or "reality"))
        {
            return;
        }

        var sockopt = stream["sockopt"] as JsonObject ?? new JsonObject();
        sockopt["dialerProxy"] = "fragment";
        stream["sockopt"] = sockopt;
    }

    private static JsonObject BuildFragmentOutbound(AppSettings settings)
    {
        var packets = string.IsNullOrWhiteSpace(settings.FragmentPackets) ? "tlshello" : settings.FragmentPackets.Trim();
        var fragment = new JsonObject
        {
            ["packets"] = packets,
            ["length"] = string.IsNullOrWhiteSpace(settings.FragmentLength) ? "50-100" : settings.FragmentLength.Trim(),
            ["interval"] = string.IsNullOrWhiteSpace(settings.FragmentInterval) ? "10-20" : settings.FragmentInterval.Trim()
        };
        if (!string.IsNullOrWhiteSpace(settings.FragmentMaxSplit))
        {
            fragment["maxSplit"] = settings.FragmentMaxSplit.Trim();
        }

        var freedomSettings = new JsonObject
        {
            ["domainStrategy"] = "AsIs",
            ["fragment"] = fragment
        };

        if (settings.NoiseEnabled)
        {
            freedomSettings["noises"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "rand",
                    ["packet"] = string.IsNullOrWhiteSpace(settings.NoiseRand) ? "10-20" : settings.NoiseRand.Trim(),
                    ["delay"] = string.IsNullOrWhiteSpace(settings.NoiseDelay) ? "10-16" : settings.NoiseDelay.Trim()
                }
            };
        }

        return new JsonObject
        {
            ["tag"] = "fragment",
            ["protocol"] = "freedom",
            ["settings"] = freedomSettings
        };
    }

    private static JsonObject BuildOutbound(ServerProfile server, AppSettings settings)
    {
        return server.Protocol switch
        {
            VpnProtocol.VMess => BuildVmess(server, settings),
            VpnProtocol.VLESS => BuildVless(server, settings),
            VpnProtocol.Trojan => BuildTrojan(server, settings),
            VpnProtocol.Shadowsocks => BuildShadowsocks(server),
            VpnProtocol.Hysteria2 => BuildHysteria2(server),
            VpnProtocol.Socks => BuildSocks(server),
            VpnProtocol.WireGuard => BuildWireGuard(server),
            _ => throw new NotSupportedException($"Protocol {server.Protocol} is not supported by Xray.")
        };
    }

    private static JsonObject BuildVmess(ServerProfile s, AppSettings settings)
    {
        var user = new JsonObject
        {
            ["id"] = s.UserId,
            ["alterId"] = 0,
            ["security"] = string.IsNullOrWhiteSpace(s.Encryption) ? "auto" : s.Encryption
        };
        var stream = BuildStreamSettings(s, settings);
        return new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "vmess",
            ["settings"] = new JsonObject
            {
                ["vnext"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = s.Address,
                        ["port"] = s.Port,
                        ["users"] = new JsonArray { user }
                    }
                }
            },
            ["streamSettings"] = stream
        };
    }

    private static JsonObject BuildVless(ServerProfile s, AppSettings settings)
    {
        var user = new JsonObject
        {
            ["id"] = s.UserId,
            ["encryption"] = "none"
        };
        if (!string.IsNullOrWhiteSpace(s.Flow))
        {
            user["flow"] = s.Flow;
        }
        return new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "vless",
            ["settings"] = new JsonObject
            {
                ["vnext"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = s.Address,
                        ["port"] = s.Port,
                        ["users"] = new JsonArray { user }
                    }
                }
            },
            ["streamSettings"] = BuildStreamSettings(s, settings)
        };
    }

    private static JsonObject BuildTrojan(ServerProfile s, AppSettings settings)
    {
        return new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "trojan",
            ["settings"] = new JsonObject
            {
                ["servers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = s.Address,
                        ["port"] = s.Port,
                        ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password
                    }
                }
            },
            ["streamSettings"] = BuildStreamSettings(s, settings)
        };
    }

    private static JsonObject BuildHysteria2(ServerProfile s)
    {
        var serverObj = new JsonObject
        {
            ["address"] = s.Address,
            ["port"] = s.Port,
            ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password
        };
        if (!string.IsNullOrWhiteSpace(s.Sni))
        {
            serverObj["sni"] = s.Sni;
        }

        return new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "hysteria2",
            ["settings"] = new JsonObject
            {
                ["servers"] = new JsonArray { serverObj }
            }
        };
    }

    private static JsonObject BuildShadowsocks(ServerProfile s)
    {
        return new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "shadowsocks",
            ["settings"] = new JsonObject
            {
                ["servers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = s.Address,
                        ["port"] = s.Port,
                        ["method"] = string.IsNullOrWhiteSpace(s.Encryption) ? "aes-256-gcm" : s.Encryption,
                        ["password"] = string.IsNullOrWhiteSpace(s.Password) ? s.UserId : s.Password
                    }
                }
            }
        };
    }

    private static JsonObject BuildSocks(ServerProfile s)
    {
        var serverObj = new JsonObject
        {
            ["address"] = s.Address,
            ["port"] = s.Port
        };

        if (!string.IsNullOrWhiteSpace(s.UserId) || !string.IsNullOrWhiteSpace(s.Password))
        {
            serverObj["users"] = new JsonArray
            {
                new JsonObject
                {
                    ["user"] = s.UserId,
                    ["pass"] = s.Password
                }
            };
        }

        return new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "socks",
            ["settings"] = new JsonObject
            {
                ["servers"] = new JsonArray { serverObj }
            }
        };
    }

    private static JsonObject BuildWireGuard(ServerProfile s)
    {
        var wg = WireGuardExtra.Parse(s.Extra);
        var peer = new JsonObject
        {
            ["publicKey"] = wg.PublicKey,
            ["endpoint"] = string.IsNullOrWhiteSpace(wg.Endpoint) ? $"{s.Address}:{s.Port}" : wg.Endpoint,
            ["allowedIPs"] = new JsonArray { "0.0.0.0/0", "::/0" }
        };

        if (wg.KeepAlive > 0)
        {
            peer["keepAlive"] = wg.KeepAlive;
        }

        if (wg.Reserved.Length == 3)
        {
            peer["reserved"] = new JsonArray { wg.Reserved[0], wg.Reserved[1], wg.Reserved[2] };
        }

        var settings = new JsonObject
        {
            ["secretKey"] = wg.SecretKey,
            ["address"] = BuildWireGuardAddresses(wg),
            ["peers"] = new JsonArray { peer },
            ["noKernelTun"] = true,
            ["domainStrategy"] = "ForceIP"
        };

        if (wg.Mtu > 0)
        {
            settings["mtu"] = wg.Mtu;
        }

        return new JsonObject
        {
            ["tag"] = "proxy",
            ["protocol"] = "wireguard",
            ["settings"] = settings
        };
    }

    private static JsonArray BuildWireGuardAddresses(WireGuardExtra wg)
    {
        var arr = new JsonArray();
        foreach (var addr in wg.Addresses)
        {
            arr.Add(addr);
        }

        if (arr.Count == 0)
        {
            arr.Add("10.0.0.2/32");
        }

        return arr;
    }

    internal sealed class WireGuardExtra
    {
        public string SecretKey { get; init; } = "";
        public string PublicKey { get; init; } = "";
        public string Endpoint { get; init; } = "";
        public List<string> Addresses { get; init; } = [];
        public int[] Reserved { get; init; } = [];
        public int Mtu { get; init; }
        public int KeepAlive { get; init; }

        public static WireGuardExtra Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new WireGuardExtra();
            }

            try
            {
                return JsonSerializer.Deserialize<WireGuardExtra>(json) ?? new WireGuardExtra();
            }
            catch
            {
                return new WireGuardExtra();
            }
        }

        public static string Serialize(WireGuardExtra extra) => JsonSerializer.Serialize(extra);
    }

    private static JsonObject BuildStreamSettings(ServerProfile s, AppSettings settings)
    {
        var network = NormalizeNetwork(s.Network);
        var stream = new JsonObject { ["network"] = network };
        var defaultFingerprint = ClientUserAgentProfiles.GetTlsFingerprint(settings.SubscriptionUserAgent);

        if (s.Security is "tls" or "reality")
        {
            stream["security"] = s.Security;
            if (s.Security == "reality")
            {
                var reality = new JsonObject
                {
                    ["serverName"] = string.IsNullOrWhiteSpace(s.Sni) ? s.Host : s.Sni,
                    ["fingerprint"] = string.IsNullOrWhiteSpace(s.Fingerprint) ? defaultFingerprint : s.Fingerprint,
                    ["show"] = false
                };
                ApplyRealityKeys(reality, s.Extra);
                stream["realitySettings"] = reality;
            }
            else
            {
                var tlsSettings = new JsonObject
                {
                    ["serverName"] = string.IsNullOrWhiteSpace(s.Sni) ? s.Host : s.Sni,
                    ["fingerprint"] = string.IsNullOrWhiteSpace(s.Fingerprint) ? defaultFingerprint : s.Fingerprint
                };
                // Xray 26+: do not emit allowInsecure (removed; true is fatal after 2026-06-01).
                if (!string.IsNullOrWhiteSpace(s.Alpn))
                {
                    var alpnArray = new JsonArray();
                    foreach (var item in s.Alpn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        alpnArray.Add(item);
                    }
                    tlsSettings["alpn"] = alpnArray;
                }
                stream["tlsSettings"] = tlsSettings;
            }
        }

        switch (network)
        {
            case "ws":
                stream["wsSettings"] = new JsonObject
                {
                    ["path"] = string.IsNullOrWhiteSpace(s.Path) ? "/" : s.Path,
                    ["headers"] = new JsonObject
                    {
                        ["Host"] = string.IsNullOrWhiteSpace(s.Host) ? s.Address : s.Host
                    }
                };
                break;
            case "xhttp":
                var xhttp = new JsonObject
                {
                    ["path"] = string.IsNullOrWhiteSpace(s.Path) ? "/" : s.Path,
                    ["host"] = string.IsNullOrWhiteSpace(s.Host) ? s.Sni : s.Host
                };
                if (!string.IsNullOrWhiteSpace(s.TransportMode))
                {
                    xhttp["mode"] = s.TransportMode;
                }
                if (!string.IsNullOrWhiteSpace(s.Extra))
                {
                    try
                    {
                        xhttp["extra"] = JsonNode.Parse(s.Extra);
                    }
                    catch
                    {
                        // ignore malformed extra
                    }
                }
                stream["xhttpSettings"] = xhttp;
                break;
            case "grpc":
                stream["grpcSettings"] = new JsonObject
                {
                    ["serviceName"] = string.IsNullOrWhiteSpace(s.Path) ? "" : s.Path
                };
                break;
            case "tcp" when !string.IsNullOrWhiteSpace(s.Host):
                stream["tcpSettings"] = new JsonObject
                {
                    ["header"] = new JsonObject
                    {
                        ["type"] = "http",
                        ["request"] = new JsonObject
                        {
                            ["headers"] = new JsonObject { ["Host"] = new JsonArray { s.Host } }
                        }
                    }
                };
                break;
        }

        return stream;
    }

    private static void ApplyRealityKeys(JsonObject reality, string extra)
    {
        if (string.IsNullOrWhiteSpace(extra))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(extra);
            var root = doc.RootElement;
            if (root.TryGetProperty("publicKey", out var pbk))
            {
                reality["publicKey"] = pbk.GetString() ?? "";
            }

            if (root.TryGetProperty("shortId", out var sid))
            {
                reality["shortId"] = sid.GetString() ?? "";
            }

            if (root.TryGetProperty("spiderX", out var spx))
            {
                reality["spiderX"] = spx.GetString() ?? "";
            }
        }
        catch
        {
            // ignore malformed extra
        }
    }

    private static JsonObject BuildDirectOutbound(AppSettings settings) => new()
    {
        ["tag"] = "direct",
        ["protocol"] = "freedom",
        ["settings"] = new JsonObject
        {
            ["domainStrategy"] = settings.PreferIpv6 ? "UseIPv6" : settings.Ipv6Enabled ? "UseIP" : "UseIPv4"
        }
    };

    private static JsonObject BuildBlockOutbound() => new()
    {
        ["tag"] = "block",
        ["protocol"] = "blackhole",
        ["settings"] = new JsonObject()
    };

    private static string NormalizeNetwork(string network) =>
        network.ToLowerInvariant() switch
        {
            "raw" or "" => "tcp",
            "httpupgrade" => "ws",
            "splithttp" => "xhttp",
            _ => network.ToLowerInvariant()
        };
}
