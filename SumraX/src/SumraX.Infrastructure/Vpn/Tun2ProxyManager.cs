using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NLog;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// tun2proxy: --setup --dns over-tcp --tun --proxy socks5://127.0.0.1:10808
/// excludes xray via antifilter split-tunnel driver; we use --bypass for the same loop prevention.
/// </summary>
public static class Tun2ProxyManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public static string InterfaceName => TunAdapterCleanup.ActiveInterfaceName;

    public static string Tun2Dir => Path.Combine(AppPaths.InstallRoot, "bin", "tun2");
    public static string BinaryPath => Path.Combine(Tun2Dir, "tun2proxy-bin.exe");
    public static string LogPath => Path.Combine(AppPaths.LogsRoot, "tun2proxy.log");

    public static bool IsAvailable =>
        OperatingSystem.IsWindows() &&
        File.Exists(BinaryPath) &&
        File.Exists(Path.Combine(Tun2Dir, "wintun.dll"));

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

        if (!File.Exists(BinaryPath))
        {
            return "tun2proxy-bin.exe not found in bin\\tun2.";
        }

        return "wintun.dll not found in bin\\tun2.";
    }

    public static Process Start(AppSettings settings, ServerProfile server, StringBuilder stderr)
    {
        if (!IsAvailable)
        {
            throw new FileNotFoundException(GetMissingReason() ?? "tun2proxy is not available.");
        }

        Directory.CreateDirectory(AppPaths.LogsRoot);
        var args = BuildArguments(settings, server);
        Logger.Info("Starting tun2proxy: {Args}", args);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = BinaryPath,
                Arguments = args,
                WorkingDirectory = Tun2Dir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) => AppendTunLog(e.Data, stderr);
        process.ErrorDataReceived += (_, e) => AppendTunLog(e.Data, stderr);

        if (!process.Start())
        {
            throw new InvalidOperationException("Failed to start tun2proxy.");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private static void AppendTunLog(string? line, StringBuilder stderr)
    {
        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        lock (stderr)
        {
            stderr.AppendLine(line);
        }

        try
        {
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // ignore log write errors
        }
    }

    public static async Task KillOrphansAsync()
    {
        if (!File.Exists(BinaryPath))
        {
            return;
        }

        var ourPath = Path.GetFullPath(BinaryPath);
        foreach (var proc in Process.GetProcessesByName("tun2proxy-bin"))
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

    /// <summary>CLI + bypass routes (uses antifilter driver for the same effect).</summary>
    public static string BuildArguments(AppSettings settings, ServerProfile? server = null)
    {
        var listen = ProxyListenHelper.GetListenAddress(settings);
        var host = listen == "0.0.0.0" ? "127.0.0.1" : listen;
        var builder = new StringBuilder();
        builder.Append("--setup ");
        builder.Append("--dns over-tcp ");
        builder.Append($"--tun {InterfaceName} ");
        builder.Append($"--proxy socks5://{host}:{settings.LocalSocksPort} ");

        foreach (var bypass in BuildBypassList(server))
        {
            builder.Append($"--bypass {bypass} ");
        }

        return builder.ToString().Trim();
    }

    private static IEnumerable<string> BuildBypassList(ServerProfile? server)
    {
        var list = new List<string> { "127.0.0.0/8" };

        if (server == null || string.IsNullOrWhiteSpace(server.Address))
        {
            return list;
        }

        if (IPAddress.TryParse(server.Address, out _))
        {
            list.Add(server.Address.Contains('/') ? server.Address : $"{server.Address}/32");
            return list;
        }

        try
        {
            var resolveTask = Dns.GetHostAddressesAsync(server.Address);
            if (!resolveTask.Wait(TimeSpan.FromSeconds(3)))
            {
                Logger.Warn("DNS resolve timed out for tun2proxy bypass: {Host}", server.Address);
                return list;
            }

            foreach (var address in resolveTask.Result)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    list.Add($"{address}/32");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Could not resolve server for tun2proxy bypass: {Host}", server.Address);
        }

        return list;
    }
}
