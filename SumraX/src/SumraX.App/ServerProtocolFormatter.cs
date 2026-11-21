using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.App;

public static class ServerProtocolFormatter
{
    public static string Format(ServerProfile profile)
    {
        var proto = profile.Protocol switch
        {
            VpnProtocol.VLESS => "VLESS",
            VpnProtocol.VMess => "VMess",
            VpnProtocol.Trojan => "Trojan",
            VpnProtocol.Shadowsocks => "SHADOWSOCKS",
            VpnProtocol.Socks => "SOCKS",
            VpnProtocol.Hysteria2 => "Hysteria2",
            VpnProtocol.Hysteria => "Hysteria",
            _ => profile.Protocol.ToString().ToUpperInvariant()
        };

        var net = string.IsNullOrWhiteSpace(profile.Network) ? "tcp" : profile.Network.ToLowerInvariant();
        var sec = profile.Security?.Trim() ?? "";
        if (string.IsNullOrEmpty(sec) || sec.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return $"{proto} / {net}";
        }

        return $"{proto} / {net} / {sec.ToLowerInvariant()}";
    }
}