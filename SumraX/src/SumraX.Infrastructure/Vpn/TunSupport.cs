namespace SumraX.Infrastructure.Vpn;

using SumraX.Core.Models;

public static class TunSupport
{
    public static bool IsAvailable() => SingBoxTunManager.IsAvailable || Tun2ProxyManager.IsAvailable;

    public static bool IsAvailable(AppSettings settings) => TunProviderSupport.IsAvailable(settings);

    public static string? GetMissingReason() =>
        SingBoxTunManager.GetMissingReason() ?? Tun2ProxyManager.GetMissingReason();

    public static string? GetMissingReason(AppSettings settings) =>
        TunProviderSupport.GetMissingReason(settings);
}
