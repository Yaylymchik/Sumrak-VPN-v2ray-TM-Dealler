using System.Diagnostics;
using System.Net.NetworkInformation;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Platform;
using SumraX.Infrastructure.Vpn;

namespace SumraX.Infrastructure.Diagnostics;

public sealed class ConflictDetectionService : IConflictDetectionService
{
    private static readonly string[] ConflictingProcessNames =
    [
        "openvpn", "wireguard", "nordvpn", "expressvpn", "protonvpn",
        "hiddify", "clash-verge", "clash", "mihomo", "v2rayn"
    ];

    private readonly ISettingsRepository _settings;
    private readonly ILocalizationService _loc;
    private readonly ISystemProxyManager _systemProxy;

    public ConflictDetectionService(
        ISettingsRepository settings,
        ILocalizationService loc,
        ISystemProxyManager systemProxy)
    {
        _settings = settings;
        _loc = loc;
        _systemProxy = systemProxy;
    }

    public async Task<ConflictReport> DetectAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<ConflictItem>();
        var settings = await _settings.GetAsync();

        foreach (var proc in Process.GetProcesses())
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var name = proc.ProcessName;
                if (ConflictingProcessNames.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                {
                    items.Add(new ConflictItem
                    {
                        Title = _loc.Format("ConflictProcess", name),
                        Detail = _loc.Get("ConflictProcessDetail"),
                        Severity = ConflictSeverity.Warning
                    });
                }
            }
            catch
            {
                // access denied for some processes
            }
            finally
            {
                proc.Dispose();
            }
        }

        if (settings.UseTunMode && TunSupport.IsAvailable() && !PlatformAdmin.IsElevated())
        {
            items.Add(new ConflictItem
            {
                Title = _loc.Get("ConflictTunAdmin"),
                Detail = _loc.Get("ConflictTunAdminDetail"),
                Severity = ConflictSeverity.Warning
            });
        }

        if (IsPortInUse(settings.LocalSocksPort) || IsPortInUse(settings.LocalHttpPort))
        {
            items.Add(new ConflictItem
            {
                Title = _loc.Get("ConflictPortsBusy"),
                Detail = _loc.Format("ConflictPortsDetail", settings.LocalSocksPort, settings.LocalHttpPort),
                Severity = ConflictSeverity.Warning
            });
        }

        var (proxyOn, proxyServer) = _systemProxy.GetProxyState();
        if (proxyOn && proxyServer is not null &&
            !proxyServer.Contains(settings.LocalHttpPort.ToString(), StringComparison.Ordinal))
        {
            items.Add(new ConflictItem
            {
                Title = _loc.Get("ConflictForeignProxy"),
                Detail = _loc.Format("ConflictForeignProxyDetail", proxyServer),
                Severity = ConflictSeverity.Info
            });
        }

        return new ConflictReport { Items = items };
    }

    private static bool IsPortInUse(int port)
    {
        var props = IPGlobalProperties.GetIPGlobalProperties();
        return props.GetActiveTcpListeners().Any(ep => ep.Port == port);
    }
}
