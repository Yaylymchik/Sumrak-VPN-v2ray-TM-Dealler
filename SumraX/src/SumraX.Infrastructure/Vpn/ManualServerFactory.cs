using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

public static class ManualServerFactory
{
    public static ServerProfile Create(
        string name,
        VpnProtocol protocol,
        string address,
        int port,
        string userId,
        string password,
        string encryption,
        string network,
        string security,
        string sni,
        string host,
        string path)
    {
        return new ServerProfile
        {
            Name = string.IsNullOrWhiteSpace(name) ? $"{protocol} Server" : name.Trim(),
            Protocol = protocol,
            Address = address.Trim(),
            Port = port,
            UserId = userId.Trim(),
            Password = password.Trim(),
            Encryption = string.IsNullOrWhiteSpace(encryption) ? DefaultEncryption(protocol) : encryption.Trim(),
            Network = string.IsNullOrWhiteSpace(network) ? "tcp" : network.Trim(),
            Security = string.IsNullOrWhiteSpace(security) ? "none" : security.Trim(),
            Sni = sni.Trim(),
            Host = host.Trim(),
            Path = path.Trim()
        };
    }

    private static string DefaultEncryption(VpnProtocol protocol) => protocol switch
    {
        VpnProtocol.VMess => "auto",
        VpnProtocol.VLESS => "none",
        VpnProtocol.Shadowsocks => "aes-256-gcm",
        VpnProtocol.Trojan => "",
        _ => "auto"
    };
}
