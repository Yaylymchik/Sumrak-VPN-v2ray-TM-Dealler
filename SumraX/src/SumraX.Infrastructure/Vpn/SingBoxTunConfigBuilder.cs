using System.Text.Json;
using System.Text.Json.Nodes;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Sumrak sing-box TUN config: tun inbound → local SOCKS (xray/sing-box core).
/// Matches common sing-box TUN client config layout.
/// </summary>
public static class SingBoxTunConfigBuilder
{
    private static readonly JsonSerializerOptions WriteOptions = JsonCompat.CreateIndentedWriteOptions();

    public static string InterfaceName => TunAdapterCleanup.ActiveInterfaceName;

    public static string Build(AppSettings settings)
    {
        var socksHost = ProxyListenHelper.GetListenAddress(settings);
        if (socksHost == "0.0.0.0")
        {
            socksHost = "127.0.0.1";
        }

        var domainResolver = new JsonObject
        {
            ["server"] = "dns-proxy",
            ["strategy"] = "prefer_ipv4"
        };

        var root = new JsonObject
        {
            ["log"] = new JsonObject
            {
                ["level"] = "info",
                ["timestamp"] = true,
                ["output"] = Path.Combine(AppPaths.LogsRoot, "sing-box-tun.log")
            },
            ["dns"] = new JsonObject
            {
                ["servers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["address"] = "8.8.8.8",
                        ["detour"] = "direct",
                        ["tag"] = "dns-proxy"
                    }
                }
            },
            ["inbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "tun",
                    ["tag"] = "tun-in",
                    ["interface_name"] = InterfaceName,
                    ["address"] = new JsonArray { "172.18.0.1/30" },
                    ["mtu"] = settings.TunMtu,
                    ["auto_route"] = true,
                    ["strict_route"] = true,
                    ["stack"] = ToStackValue(settings.TunStackMode)
                }
            },
            ["outbounds"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "socks",
                    ["tag"] = "proxy",
                    ["server"] = socksHost,
                    ["server_port"] = settings.LocalSocksPort,
                    ["udp_fragment"] = true,
                    ["domain_resolver"] = JsonCompat.DeepClone(domainResolver)
                },
                new JsonObject
                {
                    ["type"] = "direct",
                    ["tag"] = "direct",
                    ["domain_resolver"] = JsonCompat.DeepClone(domainResolver)
                }
            },
            ["route"] = new JsonObject
            {
                ["auto_detect_interface"] = true,
                ["final"] = "proxy",
                ["rules"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["outbound"] = "direct",
                        ["process_name"] = new JsonArray
                        {
                            "xray.exe",
                            "sing-box.exe",
                            Path.GetFileName(AppPaths.XrayPath),
                            Path.GetFileName(AppPaths.SingBoxPath)
                        }
                    },
                    new JsonObject { ["action"] = "sniff" },
                    new JsonObject
                    {
                        ["action"] = "hijack-dns",
                        ["protocol"] = "dns"
                    }
                }
            }
        };

        return root.ToJsonString(WriteOptions);
    }

    private static string ToStackValue(TunStackMode mode) => mode switch
    {
        TunStackMode.System => "system",
        TunStackMode.GVisor => "gvisor",
        _ => "mixed"
    };

    /// <summary>OS-level DNS on the TUN interface (sets 1.1.1.1).</summary>
    public static string GetInterfaceDns(AppSettings settings)
    {
        if (!settings.TunDnsEnable)
        {
            return string.Empty;
        }

        var dns = string.IsNullOrWhiteSpace(settings.DnsServer) ? "1.1.1.1" : settings.DnsServer.Trim();
        if (dns.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            dns.StartsWith("tls://", StringComparison.OrdinalIgnoreCase) ||
            dns.StartsWith("h3://", StringComparison.OrdinalIgnoreCase))
        {
            return "1.1.1.1";
        }

        return dns.Split('/')[0].Split(':')[0];
    }
}
