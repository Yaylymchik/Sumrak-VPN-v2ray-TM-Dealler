using SumraX.Core.Enums;
using SumraX.Infrastructure.Vpn;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public class V2rayNSubscriptionParserTests
{
    [Fact]
    public void Parse_Base64NewlineLinks_ReturnsTwoServers()
    {
        var raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            "vless://bf000000-0000-0000-0000-000000000001@1.2.3.4:443?encryption=none&security=tls&type=ws#A\r\n" +
            "trojan://pass@5.6.7.8:443?sni=example.com#B"));

        var servers = V2rayNSubscriptionParser.Parse(raw);

        Assert.Equal(2, servers.Count);
        Assert.Contains(servers, s => s.Protocol == VpnProtocol.VLESS);
        Assert.Contains(servers, s => s.Protocol == VpnProtocol.Trojan);
    }

    [Fact]
    public void Parse_ConcatenatedLinks_ReturnsTwoServers()
    {
        var vmessJson = """{"v":"2","ps":"B","add":"2.3.4.5","port":"443","id":"bf000000-0000-0000-0000-000000000002","aid":"0","net":"ws","type":"none","host":"","path":"/","tls":"tls"}""";
        var raw = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            "vless://bf000000-0000-0000-0000-000000000001@1.2.3.4:443?encryption=none&security=tls&type=ws#A" +
            "vmess://" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(vmessJson))));

        var servers = V2rayNSubscriptionParser.Parse(raw);

        Assert.Equal(2, servers.Count);
    }

    [Fact]
    public void Parse_Sip008_ReturnsShadowsocks()
    {
        var json = """{"version":1,"servers":[{"server":"ss.example.com","server_port":8388,"password":"pass","method":"aes-256-gcm","remarks":"SIP008"}]}""";
        var servers = V2rayNSubscriptionParser.Parse(json);

        Assert.Single(servers);
        Assert.Equal(VpnProtocol.Shadowsocks, servers[0].Protocol);
        Assert.Equal("ss.example.com", servers[0].Address);
    }

    [Fact]
    public void Parse_Ssd_ReturnsServer()
    {
        var payload = "ssd://" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            """{"airport":"Test","port":443,"encryption":"aes-256-gcm","password":"secret","servers":[["node.example.com","Node1"]]}"""));
        var servers = V2rayNSubscriptionParser.Parse(payload);

        Assert.Single(servers);
        Assert.Equal("node.example.com", servers[0].Address);
    }
}

public class SubscriptionUserInfoParserTests
{
    [Fact]
    public void Parse_ValidHeader_ReturnsTraffic()
    {
        var info = SubscriptionUserInfoParser.Parse(
            "upload=1000; download=2000; total=1073741824; expire=4102444800",
            "https://example.com/sub");

        Assert.NotNull(info);
        Assert.Equal(1000, info!.UploadBytes);
        Assert.Equal(2000, info.DownloadBytes);
        Assert.Equal(1073741824, info.TotalBytes);
        Assert.NotNull(info.ExpireUtc);
    }
}
