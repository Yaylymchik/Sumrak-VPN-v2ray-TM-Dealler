using System.Diagnostics;
using NLog;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Removes orphaned Wintun adapters (SumraX 4, BLOCK, sumrax-tun, …) left after abrupt disconnects.
/// </summary>
public static class TunAdapterCleanup
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public const string ActiveInterfaceName = "Sumrak";

    public static Task CleanupOrphanedAsync(string? keepName = null) =>
        Task.Run(() => CleanupOrphaned(keepName));

    /// <summary>Remove every stale Wintun adapter before creating a fresh interface.</summary>
    public static Task CleanupAllWintunAsync() =>
        Task.Run(CleanupAllWintun);

    public static void CleanupAllWintun() => CleanupOrphaned(keepName: null, removeAll: true);

    public static void CleanupOrphaned(string? keepName = null, bool removeAll = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var keep = removeAll ? null : (keepName ?? ActiveInterfaceName);
        var removed = 0;

        try
        {
            foreach (var adapter in EnumerateStaleAdapters(keep))
            {
                if (TryRemoveAdapter(adapter))
                {
                    removed++;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "TUN adapter cleanup failed");
        }

        if (removed > 0)
        {
            Logger.Info("Removed {Count} stale TUN adapter(s)", removed);
        }
    }

    private static IEnumerable<string> EnumerateStaleAdapters(string? keepName)
    {
        var script = """
            $keep = $env:SUMRAX_KEEP_IF
            Get-NetAdapter -ErrorAction SilentlyContinue |
              Where-Object {
                ($_.InterfaceDescription -like '*Wintun*' -or
                 $_.Name -like 'SumraX*' -or
                 $_.Name -like 'sumrax*' -or
                 $_.Name -eq 'BLOCK' -or
                 $_.Name -eq 'Sumrak') -and
                ($null -eq $keep -or $keep -eq '' -or $_.Name -ne $keep)
              } |
              Select-Object -ExpandProperty Name
            """;

        var output = RunPowerShell(script, keepName ?? string.Empty);
        if (string.IsNullOrWhiteSpace(output))
        {
            yield break;
        }

        foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                yield return line;
            }
        }
    }

    private static bool TryRemoveAdapter(string name)
    {
        try
        {
            Logger.Info("Removing stale TUN adapter: {Name}", name);

            var script = """
                $name = $env:SUMRAX_ADAPTER
                Disable-NetAdapter -Name $name -Confirm:$false -ErrorAction SilentlyContinue
                Start-Sleep -Milliseconds 400
                Remove-NetAdapter -Name $name -Confirm:$false -ErrorAction SilentlyContinue
                """;

            RunPowerShell(script, adapterName: name);

            if (!AdapterExists(name))
            {
                return true;
            }

            RunNetsh($"interface set interface \"{name}\" admin=disable");
            RunPowerShell(script, adapterName: name);

            if (!AdapterExists(name))
            {
                return true;
            }

            Logger.Warn("Could not fully remove adapter {Name}", name);
            return false;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to remove adapter {Name}", name);
            return false;
        }
    }

    private static bool AdapterExists(string name)
    {
        var script = """
            $name = $env:SUMRAX_ADAPTER
            if (Get-NetAdapter -Name $name -ErrorAction SilentlyContinue) { 'yes' }
            """;
        return RunPowerShell(script, adapterName: name).Contains("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static string RunPowerShell(string script, string? keepName = null, string? adapterName = null)
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

        psi.Environment["SUMRAX_KEEP_IF"] = keepName ?? string.Empty;

        if (!string.IsNullOrEmpty(adapterName))
        {
            psi.Environment["SUMRAX_ADAPTER"] = adapterName;
        }

        using var process = Process.Start(psi);
        if (process == null)
        {
            return string.Empty;
        }

        process.StandardInput.Write(script);
        process.StandardInput.Close();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit(20000);
        return stdout.Trim();
    }

    private static void RunNetsh(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "netsh.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        });
        process?.WaitForExit(5000);
    }
}
