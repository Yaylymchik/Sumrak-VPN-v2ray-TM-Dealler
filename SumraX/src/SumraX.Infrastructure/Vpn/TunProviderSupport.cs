using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

public static class TunProviderSupport
{
    public static bool UsesSingBox(TunProvider provider) =>
        provider is TunProvider.SumraXTun or TunProvider.SingBox;

    public static bool IsAvailable(TunProvider provider) => provider switch
    {
        TunProvider.Tun2Proxy => Tun2ProxyManager.IsAvailable,
        _ => SingBoxTunManager.IsAvailable
    };

    public static bool IsAvailable(AppSettings settings) =>
        IsAvailable(settings.TunProvider);

    public static string? GetMissingReason(TunProvider provider) => provider switch
    {
        TunProvider.Tun2Proxy => Tun2ProxyManager.GetMissingReason(),
        _ => SingBoxTunManager.GetMissingReason()
    };

    public static string? GetMissingReason(AppSettings settings) =>
        GetMissingReason(settings.TunProvider);

    public static Task KillOrphansAsync(TunProvider provider)
    {
        if (UsesSingBox(provider))
        {
            return SingBoxTunManager.KillOrphansAsync();
        }

        return Tun2ProxyManager.KillOrphansAsync();
    }

    public static Task KillAllOrphansAsync() =>
        Task.WhenAll(SingBoxTunManager.KillOrphansAsync(), Tun2ProxyManager.KillOrphansAsync());
}
