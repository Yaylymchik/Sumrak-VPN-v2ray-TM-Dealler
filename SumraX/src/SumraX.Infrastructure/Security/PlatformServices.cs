using System.Diagnostics;
using NLog;
using SumraX.Core.Interfaces;
using SumraX.Infrastructure.Security;

namespace SumraX.Infrastructure.Security;

/// <summary>
/// Blocks all outbound traffic except VPN core when enabled (Windows Firewall).
/// </summary>
public sealed class WindowsKillSwitchService : IKillSwitchService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string RuleName = "Sumrak VPN Kill Switch";

    public bool IsEnabled { get; private set; }

    public async Task EnableAsync()
    {
        await RunNetshAsync($"advfirewall firewall add rule name=\"{RuleName}\" dir=out action=block enable=yes");
        IsEnabled = true;
        Logger.Info("Kill Switch enabled");
    }

    public async Task DisableAsync()
    {
        await RunNetshAsync($"advfirewall firewall delete rule name=\"{RuleName}\"");
        IsEnabled = false;
        Logger.Info("Kill Switch disabled");
    }

    private static async Task RunNetshAsync(string args)
    {
        try
        {
            var psi = new ProcessStartInfo(TrustedBinaryGuard.GetSystem32Binary("netsh.exe"), args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var process = Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Kill Switch netsh command failed");
        }
    }
}

public sealed class MacKillSwitchService : IKillSwitchService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public bool IsEnabled { get; private set; }

    public Task EnableAsync()
    {
        IsEnabled = true;
        Logger.Warn("Kill Switch is not implemented on macOS yet; traffic may leak if VPN drops");
        return Task.CompletedTask;
    }

    public Task DisableAsync()
    {
        IsEnabled = false;
        return Task.CompletedTask;
    }
}

public sealed class WindowsAutoStartService : IAutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Sumrak VPN";

    public bool IsEnabled
    {
        get
        {
            var value = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey)?.GetValue(ValueName);
            return value != null;
        }
    }

    public Task SetEnabledAsync(bool enabled)
    {
        var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (key == null)
        {
            return Task.CompletedTask;
        }

        if (enabled)
        {
            var exe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                key.SetValue(ValueName, $"\"{exe}\"");
            }
        }
        else
        {
            key.DeleteValue(ValueName, false);
        }

        return Task.CompletedTask;
    }
}

public sealed class MacAutoStartService : IAutoStartService
{
    private static readonly string LaunchAgentDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents");
    private static readonly string PlistPath = Path.Combine(LaunchAgentDir, "com.sumrax.vpn.plist");
    private const string Label = "com.sumrax.vpn";

    public bool IsEnabled => File.Exists(PlistPath);

    public Task SetEnabledAsync(bool enabled)
    {
        Directory.CreateDirectory(LaunchAgentDir);

        if (enabled)
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                return Task.CompletedTask;
            }

            var plist = $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
                <plist version="1.0">
                <dict>
                    <key>Label</key>
                    <string>{Label}</string>
                    <key>ProgramArguments</key>
                    <array>
                        <string>{exe}</string>
                    </array>
                    <key>RunAtLoad</key>
                    <true/>
                    <key>KeepAlive</key>
                    <false/>
                </dict>
                </plist>
                """;
            File.WriteAllText(PlistPath, plist);
            RunLaunchCtl("load", PlistPath);
        }
        else if (File.Exists(PlistPath))
        {
            RunLaunchCtl("unload", PlistPath);
            File.Delete(PlistPath);
        }

        return Task.CompletedTask;
    }

    private static void RunLaunchCtl(string action, string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo("/bin/launchctl", $"{action} \"{path}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            })?.WaitForExit();
        }
        catch
        {
            // launchctl may fail if agent was never loaded
        }
    }
}

public sealed class XrayProcessGuard
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public bool ValidateCoreBinary()
    {
        try
        {
            TrustedBinaryGuard.EnsureTrustedCore(AppPaths.XrayPath);
            if (OperatingSystem.IsWindows())
            {
                var version = FileVersionInfo.GetVersionInfo(AppPaths.XrayPath);
                Logger.Info("Xray core detected: {Product} {Version}", version.ProductName, version.FileVersion);
            }
            else
            {
                Logger.Info("Xray core detected at {Path}", AppPaths.XrayPath);
            }

            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Invalid Xray binary");
            return false;
        }
    }
}
