using System.Diagnostics;
using System.Text;
using NLog;
using SumraX.Core.Models;
using SumraX.Infrastructure.Security;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Sumrak TUN: separate sing-box process with tun inbound → local SOCKS.
/// </summary>
public static class SingBoxTunManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static string ConfigPath => AppPaths.SingBoxTunConfigPath;

    public static bool IsAvailable =>
        OperatingSystem.IsWindows() &&
        File.Exists(AppPaths.SingBoxPath) &&
        File.Exists(Path.Combine(Path.GetDirectoryName(AppPaths.SingBoxPath)!, "wintun.dll"));

    public static string? GetMissingReason()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "TUN is supported on Windows only.";
        }

        if (IsAvailable)
        {
            return null;
        }

        if (!File.Exists(AppPaths.SingBoxPath))
        {
            return "sing-box.exe not found in bin\\sing-box.";
        }

        return "wintun.dll not found in bin\\sing-box.";
    }

    public static async Task<string> WriteConfigAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var json = SingBoxTunConfigBuilder.Build(settings);
        await File.WriteAllTextAsync(ConfigPath, json, cancellationToken);
        return ConfigPath;
    }

    public static Process Start(AppSettings settings, StringBuilder stderr)
    {
        if (!IsAvailable)
        {
            throw new FileNotFoundException(GetMissingReason() ?? "sing-box TUN is not available.");
        }

        TrustedBinaryGuard.EnsureTrustedCore(AppPaths.SingBoxPath);

        var configPath = ConfigPath;
        var singBoxDir = Path.GetDirectoryName(AppPaths.SingBoxPath)!;
        var args = $"run -c \"{configPath}\" -D \"{singBoxDir}\" --disable-color";
        Logger.Info("Starting sing-box TUN: {Args}", args);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = AppPaths.SingBoxPath,
                Arguments = args,
                WorkingDirectory = singBoxDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        process.StartInfo.Environment["ENABLE_DEPRECATED_LEGACY_DNS_SERVERS"] = "true";

        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                lock (stderr)
                {
                    stderr.AppendLine(e.Data);
                }
            }
        };

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start sing-box TUN.");
        }

        process.BeginErrorReadLine();
        return process;
    }

    public static async Task<bool> WaitForInterfaceAsync(Process? tunProcess = null, CancellationToken cancellationToken = default)
    {
        var name = SingBoxTunConfigBuilder.InterfaceName;
        for (var i = 0; i < 150; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (tunProcess is { HasExited: true })
            {
                Logger.Warn("TUN process exited before interface {Name} came up", name);
                return false;
            }

            if (IsInterfaceUp(name))
            {
                Logger.Info("TUN interface {Name} is UP", name);
                return true;
            }

            await Task.Delay(100, cancellationToken);
        }

        Logger.Warn("TUN interface {Name} did not come up within timeout", name);
        return false;
    }

    public static void ConfigureInterfaceDns(AppSettings settings)
    {
        if (!settings.TunDnsEnable)
        {
            return;
        }

        var iface = SingBoxTunConfigBuilder.InterfaceName;
        var dns = SingBoxTunConfigBuilder.GetInterfaceDns(settings);
        if (string.IsNullOrWhiteSpace(dns))
        {
            return;
        }

        Logger.Info("Setting DNS {Dns} on interface {Iface}", dns, iface);

        var script = """
            $iface = $env:SUMRAX_IFACE
            $dns = $env:SUMRAX_DNS
            Set-DnsClientServerAddress -InterfaceAlias $iface -ServerAddresses @($dns) -ErrorAction SilentlyContinue
            """;

        RunPowerShell(script, iface, dns);
    }

    public static async Task KillOrphansAsync()
    {
        if (!File.Exists(AppPaths.SingBoxPath))
        {
            return;
        }

        var ourPath = Path.GetFullPath(AppPaths.SingBoxPath);
        foreach (var proc in Process.GetProcessesByName("sing-box"))
        {
            try
            {
                var path = proc.MainModule?.FileName;
                if (path != null &&
                    string.Equals(Path.GetFullPath(path), ourPath, StringComparison.OrdinalIgnoreCase) &&
                    !proc.HasExited)
                {
                    proc.Kill(entireProcessTree: true);
                    await proc.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));
                }
            }
            catch
            {
                // ignore inaccessible processes
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    private static bool IsInterfaceUp(string name)
    {
        var script = """
            $name = $env:SUMRAX_IFACE
            $a = Get-NetAdapter -Name $name -ErrorAction SilentlyContinue
            if ($a -and $a.Status -eq 'Up') { 'up' }
            """;
        return RunPowerShell(script, name, null).Contains("up", StringComparison.OrdinalIgnoreCase);
    }

    private static string RunPowerShell(string script, string iface, string? dns)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command -",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        psi.Environment["SUMRAX_IFACE"] = iface;
        if (!string.IsNullOrEmpty(dns))
        {
            psi.Environment["SUMRAX_DNS"] = dns;
        }

        using var process = Process.Start(psi);
        if (process == null)
        {
            return string.Empty;
        }

        process.StandardInput.Write(script);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit(15000);
        return stdout.Trim();
    }
}
