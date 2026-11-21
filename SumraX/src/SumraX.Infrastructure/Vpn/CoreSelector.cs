using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

public enum VpnCoreKind
{
    Xray,
    SingBox
}

public static class CoreSelector
{
    /// <summary>
    /// Sumrak TUN: core runs as local SOCKS/HTTP; separate sing-box owns the TUN interface.
    /// </summary>
    public static bool UseTunMode(AppSettings settings, bool forceProxyMode = false) =>
        !forceProxyMode && settings.UseTunMode && TunSupport.IsAvailable();

    public static bool IsSupported(VpnProtocol protocol) => protocol switch
    {
        VpnProtocol.Tuic or VpnProtocol.ShadowsocksR or VpnProtocol.Hysteria => true,
        VpnProtocol.VMess or VpnProtocol.VLESS or VpnProtocol.Trojan or VpnProtocol.Shadowsocks
            or VpnProtocol.Hysteria2 or VpnProtocol.Socks or VpnProtocol.WireGuard => true,
        _ => false
    };

    public static VpnCoreKind Select(ServerProfile server, AppSettings settings, bool forceProxyMode = false) =>
        Select(server.Protocol);

    public static VpnCoreKind Select(VpnProtocol protocol) => protocol switch
    {
        VpnProtocol.Tuic or VpnProtocol.ShadowsocksR or VpnProtocol.Hysteria => VpnCoreKind.SingBox,
        VpnProtocol.VMess or VpnProtocol.VLESS or VpnProtocol.Trojan or VpnProtocol.Shadowsocks
            or VpnProtocol.Hysteria2 or VpnProtocol.Socks or VpnProtocol.WireGuard => VpnCoreKind.Xray,
        _ => throw new NotSupportedException($"Protocol not supported: {protocol}")
    };

    public static string GetCorePath(VpnCoreKind core) => core switch
    {
        VpnCoreKind.SingBox => AppPaths.SingBoxPath,
        _ => AppPaths.XrayPath
    };

    public static string GetConfigPath(VpnCoreKind core) => core switch
    {
        VpnCoreKind.SingBox => AppPaths.SingBoxConfigPath,
        _ => AppPaths.ConfigPath
    };

    public static string BuildConfig(ServerProfile server, AppSettings settings, bool forceProxyMode = false)
    {
        var proxyOnly = forceProxyMode || UseTunMode(settings, forceProxyMode: false);
        return Select(server, settings, forceProxyMode) switch
        {
            VpnCoreKind.SingBox => SingBoxConfigBuilder.Build(server, settings, forceProxyMode: proxyOnly),
            _ => XrayConfigBuilder.Build(server, settings, forceProxyMode: proxyOnly)
        };
    }
}
