using System.Net;
using System.Text.Json.Nodes;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

public static class RoutingRulesBuilder
{
    private static readonly string[] RuSuffixes = [".ru", ".su", ".xn--p1ai"];
    private static readonly string[] CnSuffixes = [".cn", ".中国"];

    public static void ApplyXrayRules(JsonArray rules, AppSettings settings, bool tunMode, ServerProfile? server = null)
    {
        AddPrivateDirectRules(rules);
        AddProxyServerBypassRulesXray(rules, server);

        if (tunMode)
        {
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["inboundTag"] = new JsonArray { "tun-in" },
                ["network"] = "tcp,udp",
                ["outboundTag"] = "proxy"
            });
        }

        if (settings.RoutingEnabled)
        {
            ApplyGeoDirectXray(rules, settings.RoutingMode);
            ApplyCustomDomainRulesXray(rules, settings.DomainDirectRules);
            ApplySplitTunnelXray(rules, settings.SplitTunnelProcesses);
        }

        if (settings.DnsLeakProtection)
        {
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["outboundTag"] = "proxy",
                ["port"] = "53"
            });
        }
    }

    public static void ApplySingBoxRules(JsonArray rules, AppSettings settings, bool tunMode = false, ServerProfile? server = null)
    {
        if (tunMode)
        {
            rules.Add(new JsonObject { ["port"] = new JsonArray { 53 }, ["action"] = "hijack-dns" });
            rules.Add(new JsonObject { ["protocol"] = "dns", ["action"] = "hijack-dns" });
            AddProxyServerBypassRules(rules, server);
        }

        rules.Add(new JsonObject { ["ip_is_private"] = true, ["outbound"] = "direct" });

        if (settings.RoutingEnabled)
        {
            ApplyGeoDirectSingBox(rules, settings.RoutingMode);
            ApplyCustomDomainRulesSingBox(rules, settings.DomainDirectRules);
            ApplySplitTunnelSingBox(rules, settings.SplitTunnelProcesses);
        }

        if (settings.DnsLeakProtection && !tunMode)
        {
            rules.Add(new JsonObject
            {
                ["port"] = new JsonArray { 53 },
                ["outbound"] = "proxy"
            });
        }
    }

    private static void AddProxyServerBypassRules(JsonArray rules, ServerProfile? server)
    {
        if (server == null || string.IsNullOrWhiteSpace(server.Address))
        {
            return;
        }

        if (IPAddress.TryParse(server.Address, out _))
        {
            rules.Add(new JsonObject
            {
                ["ip_cidr"] = new JsonArray { $"{server.Address}/32" },
                ["outbound"] = "direct"
            });
            return;
        }

        rules.Add(new JsonObject
        {
            ["domain"] = new JsonArray { server.Address },
            ["outbound"] = "direct"
        });
    }

    private static void AddProxyServerBypassRulesXray(JsonArray rules, ServerProfile? server)
    {
        if (server == null || string.IsNullOrWhiteSpace(server.Address))
        {
            return;
        }

        if (IPAddress.TryParse(server.Address, out _))
        {
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["ip"] = new JsonArray { server.Address },
                ["outboundTag"] = "direct"
            });
            return;
        }

        rules.Add(new JsonObject
        {
            ["type"] = "field",
            ["domain"] = new JsonArray { server.Address },
            ["outboundTag"] = "direct"
        });
    }

    private static void AddPrivateDirectRules(JsonArray rules)
    {
        rules.Add(new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct",
            ["ip"] = new JsonArray
            {
                "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12",
                "192.168.0.0/16", "::1/128", "fc00::/7"
            }
        });
    }

    private static void ApplyGeoDirectXray(JsonArray rules, RoutingMode mode)
    {
        switch (mode)
        {
            case RoutingMode.DirectRu:
                rules.Add(GeoRule("geosite:category-ru", "geoip:ru"));
                break;
            case RoutingMode.DirectCn:
                rules.Add(GeoRule("geosite:cn", "geoip:cn"));
                break;
            case RoutingMode.DirectRuCn:
                rules.Add(GeoRule("geosite:category-ru", "geoip:ru"));
                rules.Add(GeoRule("geosite:cn", "geoip:cn"));
                break;
        }

        if (mode is RoutingMode.DirectRu or RoutingMode.DirectRuCn)
        {
            AddSuffixDirectXray(rules, RuSuffixes);
        }

        if (mode is RoutingMode.DirectCn or RoutingMode.DirectRuCn)
        {
            AddSuffixDirectXray(rules, CnSuffixes);
        }
    }

    private static JsonObject GeoRule(string geosite, string geoip) => new()
    {
        ["type"] = "field",
        ["outboundTag"] = "direct",
        ["domain"] = new JsonArray { geosite },
        ["ip"] = new JsonArray { geoip }
    };

    private static void AddSuffixDirectXray(JsonArray rules, IEnumerable<string> suffixes)
    {
        var domains = new JsonArray();
        foreach (var suffix in suffixes)
        {
            domains.Add($"domain:{suffix.TrimStart('.')}");
        }

        rules.Add(new JsonObject
        {
            ["type"] = "field",
            ["outboundTag"] = "direct",
            ["domain"] = domains
        });
    }

    private static void ApplyGeoDirectSingBox(JsonArray rules, RoutingMode mode)
    {
        if (mode is RoutingMode.DirectRu or RoutingMode.DirectRuCn)
        {
            AddSuffixDirectSingBox(rules, RuSuffixes);
        }

        if (mode is RoutingMode.DirectCn or RoutingMode.DirectRuCn)
        {
            AddSuffixDirectSingBox(rules, CnSuffixes);
        }
    }

    private static void AddSuffixDirectSingBox(JsonArray rules, IEnumerable<string> suffixes)
    {
        var arr = new JsonArray();
        foreach (var suffix in suffixes)
        {
            arr.Add(suffix);
        }

        rules.Add(new JsonObject
        {
            ["domain_suffix"] = arr,
            ["outbound"] = "direct"
        });
    }

    private static void ApplyCustomDomainRulesXray(JsonArray rules, string raw)
    {
        foreach (var suffix in ParseLines(raw))
        {
            rules.Add(new JsonObject
            {
                ["type"] = "field",
                ["outboundTag"] = "direct",
                ["domain"] = new JsonArray { suffix.StartsWith("domain:", StringComparison.Ordinal) ? suffix : $"domain:{suffix}" }
            });
        }
    }

    private static void ApplyCustomDomainRulesSingBox(JsonArray rules, string raw)
    {
        var lines = ParseLines(raw).ToList();
        if (lines.Count == 0)
        {
            return;
        }

        var arr = new JsonArray();
        foreach (var line in lines)
        {
            arr.Add(line.StartsWith('.') ? line : $".{line}");
        }

        rules.Add(new JsonObject { ["domain_suffix"] = arr, ["outbound"] = "direct" });
    }

    private static void ApplySplitTunnelXray(JsonArray rules, List<string> processes)
    {
        // Xray does not support per-process routing on Windows; split tunnel via sing-box TUN path.
        _ = rules;
        _ = processes;
    }

    private static void ApplySplitTunnelSingBox(JsonArray rules, List<string> processes)
    {
        if (processes.Count == 0)
        {
            return;
        }

        var arr = new JsonArray();
        foreach (var proc in processes)
        {
            var name = proc.Trim();
            if (name.Length == 0)
            {
                continue;
            }

            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                name += ".exe";
            }

            arr.Add(name);
        }

        if (arr.Count > 0)
        {
            rules.Add(new JsonObject { ["process_name"] = arr, ["outbound"] = "direct" });
        }
    }

    private static IEnumerable<string> ParseLines(string raw) =>
        raw.Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0);
}
