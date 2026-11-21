using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public class SubscriptionProfileKeyTests
{
    [Fact]
    public void Parse_KeepsDifferentlyNamedSameEndpoint()
    {
        var body =
            "vless://03bed37d-721f-42cc-8358-f1851b7222f5@89.187.162.249:443?path=%2F&security=tls&type=ws&host=a.example&sni=a.example#UK-Mercury\n" +
            "vless://03bed37d-721f-42cc-8358-f1851b7222f5@89.187.162.249:443?path=%2F&security=tls&type=ws&host=a.example&sni=a.example#UK-Avalon\n" +
            "vless://03bed37d-721f-42cc-8358-f1851b7222f5@127.0.0.1:81?type=ws#Local-Skip\n";

        var servers = V2rayNSubscriptionParser.Parse(body);
        Assert.Equal(2, servers.Count);
        Assert.Contains(servers, s => s.Name.Contains("Mercury", StringComparison.Ordinal));
        Assert.Contains(servers, s => s.Name.Contains("Avalon", StringComparison.Ordinal));
    }

    [Fact]
    public void EndpointKey_Collapses_Name_But_ProfileKey_Does_Not()
    {
        var a = new ServerProfile
        {
            Protocol = Core.Enums.VpnProtocol.VLESS,
            Address = "1.2.3.4",
            Port = 443,
            UserId = "uuid",
            Network = "ws",
            Path = "/",
            Host = "h",
            Sni = "h",
            Name = "One"
        };
        var b = new ServerProfile
        {
            Protocol = a.Protocol,
            Address = a.Address,
            Port = a.Port,
            UserId = a.UserId,
            Network = a.Network,
            Path = a.Path,
            Host = a.Host,
            Sni = a.Sni,
            Name = "Two"
        };

        // Mirror production key formulas (keep in sync with SubscriptionRefreshService).
        static string ProfileKey(ServerProfile s) =>
            $"{s.Protocol}|{s.Address}|{s.Port}|{s.UserId}|{s.Password}|{s.Network}|{s.Path}|{s.Host}|{s.Sni}|{s.Name}";
        static string EndpointKey(ServerProfile s) =>
            $"{s.Protocol}|{s.Address}|{s.Port}|{s.UserId}|{s.Password}|{s.Network}|{s.Path}|{s.Host}|{s.Sni}";

        Assert.Equal(EndpointKey(a), EndpointKey(b));
        Assert.NotEqual(ProfileKey(a), ProfileKey(b));
    }
}
