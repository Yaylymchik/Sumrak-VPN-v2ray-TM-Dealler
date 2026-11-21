using System.Diagnostics;
using System.Text.Json;
using NLog;
using SumraX.Core.Interfaces;

namespace SumraX.Infrastructure.Vpn;

public sealed class MacSystemProxyManager : ISystemProxyManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string BackupFileName = "proxy-backup.json";

    private static string BackupPath => Path.Combine(AppPaths.DataRoot, BackupFileName);
    private static string ScriptPath => Path.Combine(AppPaths.DataRoot, "proxy_set_osx.sh");

    public async Task SetProxyAsync(string host, int port, bool bypassLocal)
    {
        await BackupCurrentIfNeededAsync();
        var bypass = bypassLocal
            ? ["localhost", "127.0.0.1", "*.local", "10.*", "192.168.*", "172.16.*", "172.17.*", "172.18.*", "172.19.*", "172.20.*", "172.21.*", "172.22.*", "172.23.*", "172.24.*", "172.25.*", "172.26.*", "172.27.*", "172.28.*", "172.29.*", "172.30.*", "172.31.*"]
            : Array.Empty<string>();
        await RunScriptAsync(["set", host, port.ToString(), .. bypass]);
        Logger.Info("macOS system proxy enabled: {Host}:{Port}", host, port);
    }

    public Task SetSocksProxyAsync(string host, int port, bool bypassLocal) =>
        SetProxyAsync(host, port, bypassLocal);

    public async Task ClearProxyAsync()
    {
        if (await TryRestoreBackupAsync())
        {
            Logger.Info("macOS system proxy restored from backup");
            return;
        }

        await RunScriptAsync(["clear"]);
        Logger.Info("macOS system proxy disabled");
    }

    public void CleanupStaleProxyOnStartup()
    {
        try
        {
            var (enabled, server) = GetProxyState();
            if (!enabled || string.IsNullOrWhiteSpace(server))
            {
                return;
            }

            if (!LooksLikeSumraXProxy(server))
            {
                return;
            }

            Logger.Warn("Stale SumraX proxy detected on startup ({Server}), clearing", server);
            _ = ClearProxyAsync();
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to cleanup stale proxy on startup");
        }
    }

    public (bool Enabled, string? Server) GetProxyState()
    {
        try
        {
            var services = ListNetworkServices();
            foreach (var service in services)
            {
                var enabled = RunNetworkSetup($"-getwebproxystate \"{service}\"", out var output) &&
                              output.Contains("Enabled: Yes", StringComparison.OrdinalIgnoreCase);
                if (!enabled)
                {
                    continue;
                }

                RunNetworkSetup($"-getwebproxy \"{service}\"", out var proxyOutput);
                var server = ParseProxyServer(proxyOutput);
                if (!string.IsNullOrWhiteSpace(server))
                {
                    return (true, server);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to read macOS proxy state");
        }

        return (false, null);
    }

    private static bool LooksLikeSumraXProxy(string server) =>
        server.Contains("127.0.0.1", StringComparison.Ordinal) &&
        (server.Contains(":10808", StringComparison.Ordinal) ||
         server.Contains(":10809", StringComparison.Ordinal) ||
         server.Contains(":20808", StringComparison.Ordinal));

    private static async Task BackupCurrentIfNeededAsync()
    {
        if (File.Exists(BackupPath))
        {
            return;
        }

        var snapshot = new ProxyBackupSnapshot();
        foreach (var service in ListNetworkServices())
        {
            RunNetworkSetup($"-getwebproxystate \"{service}\"", out var webState);
            RunNetworkSetup($"-getwebproxy \"{service}\"", out var webProxy);
            RunNetworkSetup($"-getsecurewebproxystate \"{service}\"", out var secureState);
            RunNetworkSetup($"-getsecurewebproxy \"{service}\"", out var secureProxy);
            RunNetworkSetup($"-getsocksfirewallproxystate \"{service}\"", out var socksState);
            RunNetworkSetup($"-getsocksfirewallproxy \"{service}\"", out var socksProxy);
            RunNetworkSetup($"-getproxybypassdomains \"{service}\"", out var bypass);

            snapshot.Services.Add(new ProxyServiceBackup
            {
                Service = service,
                WebEnabled = webState.Contains("Enabled: Yes", StringComparison.OrdinalIgnoreCase),
                WebProxy = webProxy,
                SecureEnabled = secureState.Contains("Enabled: Yes", StringComparison.OrdinalIgnoreCase),
                SecureProxy = secureProxy,
                SocksEnabled = socksState.Contains("Enabled: Yes", StringComparison.OrdinalIgnoreCase),
                SocksProxy = socksProxy,
                BypassDomains = bypass
            });
        }

        await File.WriteAllTextAsync(BackupPath, JsonSerializer.Serialize(snapshot));
    }

    private static async Task<bool> TryRestoreBackupAsync()
    {
        if (!File.Exists(BackupPath))
        {
            return false;
        }

        var json = await File.ReadAllTextAsync(BackupPath);
        var snapshot = JsonSerializer.Deserialize<ProxyBackupSnapshot>(json);
        if (snapshot?.Services == null)
        {
            File.Delete(BackupPath);
            return false;
        }

        foreach (var item in snapshot.Services)
        {
            RestoreService(item);
        }

        File.Delete(BackupPath);
        return true;
    }

    private static void RestoreService(ProxyServiceBackup item)
    {
        ApplyProxyState(item.Service, item.WebEnabled, item.WebProxy, "-setwebproxy", "-setwebproxystate");
        ApplyProxyState(item.Service, item.SecureEnabled, item.SecureProxy, "-setsecurewebproxy", "-setsecurewebproxystate");
        ApplyProxyState(item.Service, item.SocksEnabled, item.SocksProxy, "-setsocksfirewallproxy", "-setsocksfirewallproxystate");

        if (!string.IsNullOrWhiteSpace(item.BypassDomains))
        {
            var domains = ParseBypassDomains(item.BypassDomains);
            if (domains.Count > 0)
            {
                RunNetworkSetup($"-setproxybypassdomains \"{item.Service}\" {string.Join(' ', domains.Select(d => $"\"{d}\""))}", out _);
            }
        }
    }

    private static void ApplyProxyState(string service, bool enabled, string proxyOutput, string setCmd, string stateCmd)
    {
        if (enabled && TryParseHostPort(proxyOutput, out var host, out var port))
        {
            RunNetworkSetup($"{setCmd} \"{service}\" \"{host}\" \"{port}\"", out _);
            RunNetworkSetup($"{stateCmd} \"{service}\" on", out _);
            return;
        }

        RunNetworkSetup($"{stateCmd} \"{service}\" off", out _);
    }

    private static async Task RunScriptAsync(IReadOnlyList<string> args)
    {
        await EnsureScriptAsync();
        var psi = new ProcessStartInfo("/bin/bash", $"\"{ScriptPath}\" {string.Join(' ', args.Select(a => $"\"{a}\""))}")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start proxy script.");
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            var err = await proc.StandardError.ReadToEndAsync();
            throw new InvalidOperationException($"proxy_set_osx.sh failed: {err}".Trim());
        }
    }

    private static async Task EnsureScriptAsync()
    {
        if (File.Exists(ScriptPath))
        {
            return;
        }

        AppPaths.EnsureDirectories();
        var script = EmbeddedResources.Read("SumraX.Infrastructure.Resources.proxy_set_osx.sh");
        await File.WriteAllTextAsync(ScriptPath, script);
        Platform.PlatformRuntime.EnsureExecutable(ScriptPath);
    }

    private static IReadOnlyList<string> ListNetworkServices()
    {
        if (!RunNetworkSetup("-listallnetworkservices", out var output))
        {
            return [];
        }

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !line.StartsWith('*'))
            .ToArray();
    }

    private static bool RunNetworkSetup(string args, out string output)
    {
        var psi = new ProcessStartInfo("/usr/sbin/networksetup", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi);
        if (proc == null)
        {
            output = "";
            return false;
        }

        output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return proc.ExitCode == 0;
    }

    private static string? ParseProxyServer(string output)
    {
        if (!TryParseHostPort(output, out var host, out var port))
        {
            return null;
        }

        return $"{host}:{port}";
    }

    private static bool TryParseHostPort(string output, out string host, out int port)
    {
        host = "";
        port = 0;
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var serverLine = lines.FirstOrDefault(l => l.StartsWith("Server:", StringComparison.OrdinalIgnoreCase));
        var portLine = lines.FirstOrDefault(l => l.StartsWith("Port:", StringComparison.OrdinalIgnoreCase));
        if (serverLine == null || portLine == null)
        {
            return false;
        }

        host = serverLine.Split(':', 2)[1].Trim();
        return int.TryParse(portLine.Split(':', 2)[1].Trim(), out port);
    }

    private static List<string> ParseBypassDomains(string output)
    {
        var marker = "There aren't any bypass domains set on";
        if (output.Contains(marker, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(1)
            .ToList();
    }

    private sealed class ProxyBackupSnapshot
    {
        public List<ProxyServiceBackup> Services { get; set; } = [];
    }

    private sealed class ProxyServiceBackup
    {
        public string Service { get; set; } = "";
        public bool WebEnabled { get; set; }
        public string WebProxy { get; set; } = "";
        public bool SecureEnabled { get; set; }
        public string SecureProxy { get; set; } = "";
        public bool SocksEnabled { get; set; }
        public string SocksProxy { get; set; } = "";
        public string BypassDomains { get; set; } = "";
    }
}
