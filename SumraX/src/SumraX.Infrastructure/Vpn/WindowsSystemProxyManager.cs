using System.Runtime.InteropServices;
using Microsoft.Win32;
using NLog;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

public sealed class WindowsSystemProxyManager : ISystemProxyManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const int InternetOptionSettingsChanged = 39;
    private const int InternetOptionRefresh = 37;
    private const string InternetSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";
    private const string BackupKey = @"Software\Sumrak VPN\ProxyBackup";
    private const string DefaultBypass =
        "localhost;127.*;10.*;172.16.*;172.17.*;172.18.*;172.19.*;172.20.*;172.21.*;172.22.*;172.23.*;172.24.*;172.25.*;172.26.*;172.27.*;172.28.*;172.29.*;172.30.*;172.31.*;192.168.*;<local>";

    [DllImport("wininet.dll", SetLastError = true)]
    private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

    public Task SetProxyAsync(string host, int port, bool bypassLocal)
    {
        BackupCurrentIfNeeded();
        var proxy = $"{host}:{port}";
        using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsKey, true);
        if (key == null)
        {
            return Task.CompletedTask;
        }

        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", proxy, RegistryValueKind.String);
        if (bypassLocal)
        {
            key.SetValue("ProxyOverride", DefaultBypass, RegistryValueKind.String);
        }

        NotifyProxyChanged();
        Logger.Info("System proxy enabled: {Proxy}, bypassLocal={Bypass}", proxy, bypassLocal);
        return Task.CompletedTask;
    }

    public Task SetSocksProxyAsync(string host, int port, bool bypassLocal)
    {
        BackupCurrentIfNeeded();
        var proxy = $"socks={host}:{port}";
        using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsKey, true);
        if (key == null)
        {
            return Task.CompletedTask;
        }

        key.SetValue("ProxyEnable", 1, RegistryValueKind.DWord);
        key.SetValue("ProxyServer", proxy, RegistryValueKind.String);
        if (bypassLocal)
        {
            key.SetValue("ProxyOverride", DefaultBypass, RegistryValueKind.String);
        }

        NotifyProxyChanged();
        Logger.Info("System SOCKS proxy enabled: {Proxy}, bypassLocal={Bypass}", proxy, bypassLocal);
        return Task.CompletedTask;
    }

    public Task ClearProxyAsync()
    {
        if (TryRestoreBackup())
        {
            NotifyProxyChanged();
            Logger.Info("System proxy restored from backup");
            return Task.CompletedTask;
        }

        using var key = Registry.CurrentUser.CreateSubKey(InternetSettingsKey, true);
        key?.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
        NotifyProxyChanged();
        Logger.Info("System proxy disabled");
        return Task.CompletedTask;
    }

    public void CleanupStaleProxyOnStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey, false);
            if (key == null)
            {
                return;
            }

            var enabled = key.GetValue("ProxyEnable");
            if (enabled is not int en || en == 0)
            {
                return;
            }

            var server = key.GetValue("ProxyServer") as string ?? "";
            if (!LooksLikeSumraXProxy(server))
            {
                return;
            }

            Logger.Warn("Stale SumraX proxy detected on startup ({Server}), clearing", server);
            if (!TryRestoreBackup())
            {
                using var writeKey = Registry.CurrentUser.CreateSubKey(InternetSettingsKey, true);
                writeKey?.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
            }

            NotifyProxyChanged();
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
            using var key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey, false);
            if (key == null)
            {
                return (false, null);
            }

            var enabled = key.GetValue("ProxyEnable") is int en && en == 1;
            var server = key.GetValue("ProxyServer") as string;
            return (enabled, server);
        }
        catch
        {
            return (false, null);
        }
    }

    private static bool LooksLikeSumraXProxy(string server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return false;
        }

        return server.Contains("127.0.0.1", StringComparison.Ordinal) &&
               (server.Contains(":10808", StringComparison.Ordinal) ||
                server.Contains(":10809", StringComparison.Ordinal) ||
                server.Contains(":20808", StringComparison.Ordinal) ||
                server.Contains("socks=", StringComparison.OrdinalIgnoreCase));
    }

    private static void BackupCurrentIfNeeded()
    {
        using var backup = Registry.CurrentUser.OpenSubKey(BackupKey, true);
        if (backup?.GetValue("HasBackup") is int has && has == 1)
        {
            return;
        }

        using var src = Registry.CurrentUser.OpenSubKey(InternetSettingsKey, false);
        if (src == null)
        {
            return;
        }

        using var dest = Registry.CurrentUser.CreateSubKey(BackupKey, true);
        if (dest == null)
        {
            return;
        }

        CopyValue(src, dest, "ProxyEnable");
        CopyValue(src, dest, "ProxyServer");
        CopyValue(src, dest, "ProxyOverride");
        CopyValue(src, dest, "AutoConfigURL");
        dest.SetValue("HasBackup", 1, RegistryValueKind.DWord);
    }

    private static bool TryRestoreBackup()
    {
        using var backup = Registry.CurrentUser.OpenSubKey(BackupKey, false);
        if (backup?.GetValue("HasBackup") is not int has || has != 1)
        {
            return false;
        }

        using var dest = Registry.CurrentUser.CreateSubKey(InternetSettingsKey, true);
        if (dest == null)
        {
            return false;
        }

        RestoreValue(backup, dest, "ProxyEnable");
        RestoreValue(backup, dest, "ProxyServer");
        RestoreValue(backup, dest, "ProxyOverride");
        RestoreValue(backup, dest, "AutoConfigURL");

        Registry.CurrentUser.DeleteSubKeyTree(BackupKey, false);
        return true;
    }

    private static void CopyValue(RegistryKey src, RegistryKey dest, string name)
    {
        var value = src.GetValue(name);
        if (value != null)
        {
            dest.SetValue(name, value);
        }
    }

    private static void RestoreValue(RegistryKey src, RegistryKey dest, string name)
    {
        var value = src.GetValue(name);
        if (value == null)
        {
            dest.DeleteValue(name, false);
        }
        else
        {
            dest.SetValue(name, value);
        }
    }

    private static void NotifyProxyChanged()
    {
        InternetSetOption(IntPtr.Zero, InternetOptionSettingsChanged, IntPtr.Zero, 0);
        InternetSetOption(IntPtr.Zero, InternetOptionRefresh, IntPtr.Zero, 0);
    }
}

public static class ProxyListenHelper
{
    public static string GetListenAddress(AppSettings settings) =>
        settings.AllowLanConnections ? "0.0.0.0" : "127.0.0.1";
}
