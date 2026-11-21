using System.Text.Json.Nodes;
using SumraX.Core.Enums;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public class RoutingRulesBuilderTests
{
    [Fact]
    public void ApplyXrayRules_when_routing_disabled_skips_custom_rules()
    {
        var rules = new JsonArray();
        var settings = new AppSettings
        {
            RoutingEnabled = false,
            RoutingMode = RoutingMode.DirectRu,
            DomainDirectRules = "bank.ru",
            SplitTunnelProcesses = ["chrome.exe"]
        };

        RoutingRulesBuilder.ApplyXrayRules(rules, settings, tunMode: false);

        var json = rules.ToJsonString();
        Assert.Contains("127.0.0.0/8", json, StringComparison.Ordinal);
        Assert.DoesNotContain("geosite:category-ru", json, StringComparison.Ordinal);
        Assert.DoesNotContain("bank.ru", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyXrayRules_when_routing_enabled_applies_custom_rules()
    {
        var rules = new JsonArray();
        var settings = new AppSettings
        {
            RoutingEnabled = true,
            RoutingMode = RoutingMode.DirectRu,
            DomainDirectRules = "bank.ru"
        };

        RoutingRulesBuilder.ApplyXrayRules(rules, settings, tunMode: false);

        var json = rules.ToJsonString();
        Assert.Contains("geosite:category-ru", json, StringComparison.Ordinal);
        Assert.Contains("bank.ru", json, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplySingBoxRules_when_routing_disabled_skips_custom_rules()
    {
        var rules = new JsonArray();
        var settings = new AppSettings
        {
            RoutingEnabled = false,
            RoutingMode = RoutingMode.DirectRu,
            DomainDirectRules = "bank.ru",
            SplitTunnelProcesses = ["chrome.exe"]
        };

        RoutingRulesBuilder.ApplySingBoxRules(rules, settings, tunMode: true);

        var json = rules.ToJsonString();
        Assert.Contains("ip_is_private", json, StringComparison.Ordinal);
        Assert.DoesNotContain("bank.ru", json, StringComparison.Ordinal);
        Assert.DoesNotContain("chrome.exe", json, StringComparison.Ordinal);
    }
}
