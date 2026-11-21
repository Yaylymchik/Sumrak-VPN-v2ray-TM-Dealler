using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public class SingBoxTunConfigBuilderTests
{
    [Fact]
    public void Build_matches_reference_tun_shape()
    {
        var settings = new AppSettings
        {
            LocalSocksPort = 10808,
            TunMtu = 1500
        };

        var json = SingBoxTunConfigBuilder.Build(settings);

        Assert.Contains("\"interface_name\": \"Sumrak\"", json, StringComparison.Ordinal);
        Assert.Contains("\"stack\": \"mixed\"", json, StringComparison.Ordinal);
        Assert.Contains("\"172.18.0.1/30\"", json, StringComparison.Ordinal);
        Assert.Contains("\"server_port\": 10808", json, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"socks\"", json, StringComparison.Ordinal);
        Assert.Contains("\"action\": \"hijack-dns\"", json, StringComparison.Ordinal);
        Assert.Contains("\"address\": \"8.8.8.8\"", json, StringComparison.Ordinal);
        Assert.Contains("xray.exe", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GetInterfaceDns_defaults_to_cloudflare()
    {
        var dns = SingBoxTunConfigBuilder.GetInterfaceDns(new AppSettings());
        Assert.Equal("1.1.1.1", dns);
    }
}
