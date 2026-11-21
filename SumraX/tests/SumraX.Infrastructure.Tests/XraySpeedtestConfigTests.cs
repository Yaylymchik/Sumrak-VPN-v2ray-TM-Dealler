using System.Text.Json.Nodes;
using SumraX.Core.Enums;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public class XraySpeedtestConfigTests
{
    [Fact]
    public void BuildForSpeedtest_has_socks_and_http_inbounds_without_tun()
    {
        var server = new ServerProfile
        {
            Name = "Ping",
            Address = "1.2.3.4",
            Port = 443,
            Protocol = VpnProtocol.VLESS,
            UserId = "00000000-0000-0000-0000-000000000001",
            Security = "tls",
            Network = "tcp",
            Sni = "example.com"
        };
        var settings = new AppSettings { MuxEnabled = true, UseTunMode = true, LocalSocksPort = 10808 };

        var json = XrayConfigBuilder.BuildForSpeedtest(server, settings, socksPort: 19080);
        var root = JsonNode.Parse(json)!.AsObject();

        Assert.Equal("error", root["log"]?["loglevel"]?.GetValue<string>());
        var inbounds = root["inbounds"]!.AsArray();
        Assert.Equal(2, inbounds.Count);
        Assert.Equal(19080, inbounds[0]!["port"]!.GetValue<int>());
        Assert.Equal("socks", inbounds[0]!["protocol"]!.GetValue<string>());
        Assert.Equal(19081, inbounds[1]!["port"]!.GetValue<int>());
        Assert.Equal("http", inbounds[1]!["protocol"]!.GetValue<string>());
        Assert.DoesNotContain("tun-in", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"mux\"", json, StringComparison.Ordinal);
        Assert.Contains("\"protocol\": \"vless\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(VpnProtocol.VMess)]
    [InlineData(VpnProtocol.Trojan)]
    [InlineData(VpnProtocol.Shadowsocks)]
    [InlineData(VpnProtocol.Hysteria2)]
    public void BuildForSpeedtest_supports_xray_protocols(VpnProtocol protocol)
    {
        var server = new ServerProfile
        {
            Name = "T",
            Address = "1.2.3.4",
            Port = 443,
            Protocol = protocol,
            UserId = "00000000-0000-0000-0000-000000000001",
            Password = "secret",
            Encryption = "aes-128-gcm",
            Security = "tls",
            Network = "tcp"
        };

        var json = XrayConfigBuilder.BuildForSpeedtest(server, new AppSettings(), 19100);
        Assert.Contains("\"http-in\"", json, StringComparison.Ordinal);
        Assert.Contains("\"proxy\"", json, StringComparison.Ordinal);
    }
}
