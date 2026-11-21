using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace SumraX.Infrastructure.Network;

/// <summary>
/// Windows Wi‑Fi hotspot / LAN proxy sharing (parity with Android VpnHotspot no-root path).
/// </summary>
public static class WindowsVpnHotspotHelper
{
    public static void OpenMobileHotspotSettings()
    {
        TryShellExecute("ms-settings:network-mobilehotspot");
    }

    public static void OpenNetworkConnections()
    {
        TryShellExecute("ncpa.cpl");
    }

    public static string DetectShareHostIpv4()
    {
        try
        {
            string? hotspot = null;
            string? wifi = null;
            string? any = null;

            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var name = ni.Name;
                var desc = ni.Description;
                var isHotspot = ContainsHotspotHint(name) || ContainsHotspotHint(desc);

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork ||
                        IPAddress.IsLoopback(addr.Address))
                    {
                        continue;
                    }

                    var ip = addr.Address.ToString();
                    any ??= ip;

                    if (isHotspot)
                    {
                        hotspot ??= ip;
                    }

                    if (ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && !isHotspot)
                    {
                        wifi ??= ip;
                    }
                }
            }

            return hotspot ?? wifi ?? any ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    public static string BuildProxySummary(
        bool vpnConnected,
        bool lanSharing,
        string hostIp,
        int socksPort,
        int httpPort)
    {
        var sb = new StringBuilder();
        sb.AppendLine(vpnConnected ? "vpn:on" : "vpn:off");
        sb.AppendLine(lanSharing ? "proxy:lan" : "proxy:local");
        if (lanSharing)
        {
            sb.AppendLine($"SOCKS5 {hostIp}:{socksPort}");
            sb.AppendLine($"HTTP {hostIp}:{httpPort}");
        }

        return sb.ToString().TrimEnd();
    }

    public static string BuildCopyBlock(string hostIp, int socksPort, int httpPort)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Sumrak VPN — hotspot / LAN proxy");
        sb.AppendLine($"SOCKS5 {hostIp}:{socksPort}");
        sb.AppendLine($"HTTP   {hostIp}:{httpPort}");
        sb.AppendLine("Clients: set manual proxy to the PC IP and ports above.");
        return sb.ToString().TrimEnd();
    }

    private static bool ContainsHotspotHint(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Hotspot", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Wi-Fi Direct", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("WiFi Direct", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Hosted Network", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Local Area Connection*", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryShellExecute(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }
}