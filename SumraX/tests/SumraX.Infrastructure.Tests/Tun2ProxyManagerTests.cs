using SumraX.Core.Enums;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public class Tun2ProxyManagerTests
{
    [Fact]
    public void BuildArguments_matches_reference_layout()
    {
        var settings = new AppSettings { LocalSocksPort = 10808 };
        var server = new ServerProfile
        {
            Name = "Test",
            Address = "108.177.14.84",
            Port = 443,
            Protocol = VpnProtocol.VLESS,
            UserId = "00000000-0000-0000-0000-000000000001"
        };

        var args = Tun2ProxyManager.BuildArguments(settings, server);

        Assert.Contains("--setup", args, StringComparison.Ordinal);
        Assert.Contains("--dns over-tcp", args, StringComparison.Ordinal);
        Assert.Contains("--tun Sumrak", args, StringComparison.Ordinal);
        Assert.Contains("--proxy socks5://127.0.0.1:10808", args, StringComparison.Ordinal);
        Assert.Contains("--bypass 127.0.0.0/8", args, StringComparison.Ordinal);
        Assert.Contains("--bypass 108.177.14.84/32", args, StringComparison.Ordinal);
    }
}

public class TunProviderSupportTests
{
    [Fact]
    public void UsesSingBox_for_sumrax_and_singbox_only()
    {
        Assert.True(TunProviderSupport.UsesSingBox(TunProvider.SumraXTun));
        Assert.True(TunProviderSupport.UsesSingBox(TunProvider.SingBox));
        Assert.False(TunProviderSupport.UsesSingBox(TunProvider.Tun2Proxy));
    }
}
