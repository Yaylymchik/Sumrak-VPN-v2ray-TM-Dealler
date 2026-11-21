using SumraX.Core.Enums;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public class SingBoxConfigBuilderTests
{
    [Fact]
    public void Build_config_has_no_legacy_inbound_fields()
    {
        var server = new ServerProfile
        {
            Name = "Kraken",
            Address = "1.2.3.4",
            Port = 443,
            Protocol = VpnProtocol.Tuic,
            UserId = "00000000-0000-0000-0000-000000000001",
            Password = "secret",
            Security = "tls",
            Sni = "example.com"
        };
        var settings = new AppSettings
        {
            UseTunMode = true,
            SniffTraffic = true,
            LocalHttpPort = 10809,
            LocalSocksPort = 10808
        };

        var json = SingBoxConfigBuilder.Build(server, settings, forceProxyMode: true);

        Assert.DoesNotContain("\"sniff\": true", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sniff\": false", json, StringComparison.Ordinal);
        Assert.DoesNotContain("inet4_address", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sniff_override_destination", json, StringComparison.Ordinal);
        Assert.Contains("\"action\": \"sniff\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_tun_inbound_uses_address_array()
    {
        var settings = new AppSettings { TunMtu = 1500, SniffTraffic = true };
        var json = SingBoxConfigBuilder.Build(new ServerProfile
        {
            Name = "T",
            Address = "1.2.3.4",
            Port = 443,
            Protocol = VpnProtocol.VLESS,
            UserId = "00000000-0000-0000-0000-000000000001",
            Security = "tls"
        }, settings);

        Assert.Contains("\"address\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("inet4_address", json, StringComparison.Ordinal);
        Assert.Contains("\"action\": \"hijack-dns\"", json, StringComparison.Ordinal);
        Assert.Contains("\"stack\": \"system\"", json, StringComparison.Ordinal);
    }
}
