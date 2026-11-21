using System.Diagnostics;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;

namespace SumraX.Infrastructure.Diagnostics;

public sealed class DiagnosticsService : IDiagnosticsService
{
    private readonly IVpnEngine _vpn;
    private readonly IServerRepository _servers;
    private readonly ISettingsRepository _settings;
    private readonly IKillSwitchService _killSwitch;
    private readonly ISystemProxyManager _systemProxy;
    private readonly ISubscriptionRefreshService _subRefresh;
    private readonly ILocalizationService _loc;

    public DiagnosticsService(
        IVpnEngine vpn,
        IServerRepository servers,
        ISettingsRepository settings,
        IKillSwitchService killSwitch,
        ISystemProxyManager systemProxy,
        ISubscriptionRefreshService subRefresh,
        ILocalizationService loc)
    {
        _vpn = vpn;
        _servers = servers;
        _settings = settings;
        _killSwitch = killSwitch;
        _systemProxy = systemProxy;
        _subRefresh = subRefresh;
        _loc = loc;
    }

    public async Task<DiagnosticReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var items = new List<DiagnosticItem>();
        var settings = await _settings.GetAsync();
        var servers = await _servers.GetAllAsync();
        var urls = await _subRefresh.GetSubscriptionUrlsAsync();
        var connected = _vpn.State == ConnectionState.Connected;

        items.Add(CheckFile(_loc.Get("DiagXrayCore"), AppPaths.XrayPath));
        items.Add(CheckFile(_loc.Get("DiagSingBoxCore"), AppPaths.SingBoxPath));

        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagServers"),
            Status = servers.Count > 0 ? DiagnosticStatus.Ok : DiagnosticStatus.Warning,
            Detail = _loc.Format("DiagServersDetail", servers.Count)
        });

        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagSubscriptions"),
            Status = urls.Count > 0 ? DiagnosticStatus.Ok : DiagnosticStatus.Warning,
            Detail = _loc.Format("DiagSubsDetail", urls.Count)
        });

        var (proxyOn, proxyServer) = _systemProxy.GetProxyState();
        var tunActive = connected && _vpn.IsTunMode;
        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagSystemProxy"),
            Status = connected
                ? tunActive
                    ? proxyOn ? DiagnosticStatus.Warning : DiagnosticStatus.Ok
                    : proxyOn ? DiagnosticStatus.Ok : DiagnosticStatus.Error
                : proxyOn ? DiagnosticStatus.Warning : DiagnosticStatus.Ok,
            Detail = tunActive
                ? _loc.Get("DiagTunActiveNoProxy")
                : proxyOn
                    ? _loc.Format("DiagProxyOn", proxyServer ?? "?")
                    : _loc.Get("DiagProxyOff")
        });

        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagTun"),
            Status = settings.UseTunMode && TunSupport.IsAvailable()
                ? DiagnosticStatus.Ok
                : settings.UseTunMode ? DiagnosticStatus.Error : DiagnosticStatus.Warning,
            Detail = settings.UseTunMode
                ? TunSupport.IsAvailable() ? _loc.Get("DiagTunReady") : TunSupport.GetMissingReason() ?? "?"
                : _loc.Get("DiagTunOff")
        });

        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagConnection"),
            Status = _vpn.State switch
            {
                ConnectionState.Connected => DiagnosticStatus.Ok,
                ConnectionState.Connecting or ConnectionState.Reconnecting => DiagnosticStatus.Warning,
                ConnectionState.Error => DiagnosticStatus.Error,
                _ => DiagnosticStatus.Warning
            },
            Detail = FormatConnectionDetail()
        });

        var portsOk = connected
            ? PortDiagnostics.IsPortListening(settings.LocalSocksPort) || PortDiagnostics.IsPortListening(settings.LocalHttpPort)
            : !PortDiagnostics.IsPortListening(settings.LocalSocksPort) && !PortDiagnostics.IsPortListening(settings.LocalHttpPort);
        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagLocalPorts"),
            Status = portsOk ? DiagnosticStatus.Ok : DiagnosticStatus.Warning,
            Detail = _loc.Format("DiagPortsDetail", settings.LocalSocksPort, settings.LocalHttpPort)
        });

        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagKillSwitch"),
            Status = settings.KillSwitch ? DiagnosticStatus.Warning : DiagnosticStatus.Ok,
            Detail = settings.KillSwitch
                ? _loc.Format("DiagKillSwitchOn", _killSwitch.IsEnabled ? _loc.Get("Active") : _loc.Get("Inactive"))
                : _loc.Get("DiagKillSwitchOff")
        });

        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagProxyMode"),
            Status = DiagnosticStatus.Ok,
            Detail = tunActive
                ? _loc.Get("DiagTunModeDetail")
                : settings.UseTunMode && TunSupport.IsAvailable()
                    ? _loc.Get("DiagTunPreferred")
                    : _loc.Get("DiagProxyModeDetail")
        });

        var logFile = Directory.Exists(AppPaths.LogsRoot)
            ? Directory.GetFiles(AppPaths.LogsRoot, "sumrax-*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        items.Add(new DiagnosticItem
        {
            Name = _loc.Get("DiagLogs"),
            Status = logFile != null ? DiagnosticStatus.Ok : DiagnosticStatus.Warning,
            Detail = logFile != null ? Path.GetFileName(logFile) : _loc.Get("DiagLogsMissing")
        });

        return new DiagnosticReport { Items = items };
    }

    private DiagnosticItem CheckFile(string name, string path)
    {
        if (!File.Exists(path))
        {
            return new DiagnosticItem
            {
                Name = name,
                Status = DiagnosticStatus.Error,
                Detail = _loc.Get("DiagMissing")
            };
        }

        try
        {
            var info = FileVersionInfo.GetVersionInfo(path);
            var version = info.FileVersion ?? info.ProductVersion ?? "?";
            return new DiagnosticItem
            {
                Name = name,
                Status = DiagnosticStatus.Ok,
                Detail = version
            };
        }
        catch
        {
            return new DiagnosticItem
            {
                Name = name,
                Status = DiagnosticStatus.Ok,
                Detail = Path.GetFileName(path)
            };
        }
    }

    private string FormatConnectionDetail()
    {
        var status = _vpn.CurrentStatus;
        var state = _loc.Get(_vpn.State switch
        {
            ConnectionState.Connected => "Protected",
            ConnectionState.Connecting => "Connecting",
            ConnectionState.Reconnecting => "Connecting",
            ConnectionState.Error => "ConnectionFailed",
            _ => "NotConnected"
        });

        if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
        {
            return $"{state} — {status.ErrorMessage}";
        }

        if (!string.IsNullOrWhiteSpace(status.ServerName))
        {
            return $"{state} — {status.ServerName}";
        }

        return state;
    }
}

internal static class PortDiagnostics
{
    public static bool IsPortListening(int port)
    {
        var props = IPGlobalProperties.GetIPGlobalProperties();
        return props.GetActiveTcpListeners().Any(ep => ep.Port == port);
    }
}

public sealed class DebugExportService : IDebugExportService
{
    private readonly IDiagnosticsService _diagnostics;
    private readonly ISettingsRepository _settings;
    private readonly IServerRepository _servers;

    public DebugExportService(
        IDiagnosticsService diagnostics,
        ISettingsRepository settings,
        IServerRepository servers)
    {
        _diagnostics = diagnostics;
        _settings = settings;
        _servers = servers;
    }

    public async Task<string> ExportBundleAsync(CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(AppPaths.LogsRoot, $"sumrax-debug-{stamp}.zip");

        var report = await _diagnostics.RunAsync(cancellationToken);
        var settings = await _settings.GetAsync();
        var serverCount = (await _servers.GetAllAsync()).Count;
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?";

        var summary = new StringBuilder();
        summary.AppendLine($"Sumrak VPN debug bundle — {DateTime.UtcNow:O}");
        summary.AppendLine($"Version: {version}");
        summary.AppendLine($"OS: {Environment.OSVersion}");
        summary.AppendLine($"Servers: {serverCount}");
        summary.AppendLine($"Subscription URLs: {settings.SubscriptionUrls.Count}");
        summary.AppendLine($"MinimizeToTray: {settings.MinimizeToTray}");
        summary.AppendLine($"KillSwitch setting: {settings.KillSwitch}");
        summary.AppendLine();
        foreach (var item in report.Items)
        {
            summary.AppendLine($"[{item.Status}] {item.Name}: {item.Detail}");
        }

        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        var entry = zip.CreateEntry("diagnostics.txt");
        await using (var writer = new StreamWriter(entry.Open()))
        {
            await writer.WriteAsync(summary.ToString());
        }

        if (Directory.Exists(AppPaths.LogsRoot))
        {
            foreach (var log in Directory.GetFiles(AppPaths.LogsRoot, "sumrax-*.log")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Take(3))
            {
                zip.CreateEntryFromFile(log, Path.GetFileName(log), CompressionLevel.Optimal);
            }
        }

        return zipPath;
    }
}
