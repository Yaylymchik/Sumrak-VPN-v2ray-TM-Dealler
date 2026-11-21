using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Infrastructure.Network;

namespace SumraX.App.ViewModels;

public partial class SettingsViewModel
{
    private readonly IVpnEngine _vpn;

    public string SectionVpnHotspotLabel { get; private set; } = "";
    public string VpnHotspotIntro { get; private set; } = "";
    public string VpnHotspotSteps { get; private set; } = "";
    public string VpnHotspotStatusVpnLabel { get; private set; } = "";
    public string VpnHotspotStatusProxyLabel { get; private set; } = "";
    public string VpnHotspotProxyLinesLabel { get; private set; } = "";
    public string VpnHotspotReconnectHint { get; private set; } = "";
    public string VpnHotspotIcsNote { get; private set; } = "";
    public string OpenWindowsHotspotLabel { get; private set; } = "";
    public string OpenNetworkAdaptersLabel { get; private set; } = "";
    public string CopyHotspotProxyLabel { get; private set; } = "";

    [ObservableProperty] private string _vpnHotspotVpnStatus = "";
    [ObservableProperty] private string _vpnHotspotProxyStatus = "";
    [ObservableProperty] private string _vpnHotspotHostIp = "—";
    [ObservableProperty] private string _vpnHotspotProxyLines = "";

    internal void ApplyVpnHotspotLabels()
    {
        SectionVpnHotspotLabel = L("SettingsRowVpnHotspot");
        VpnHotspotIntro = L("VpnHotspotIntro");
        VpnHotspotSteps = L("VpnHotspotSteps");
        VpnHotspotStatusVpnLabel = L("VpnHotspotStatusVpn");
        VpnHotspotStatusProxyLabel = L("VpnHotspotStatusProxy");
        VpnHotspotProxyLinesLabel = L("VpnHotspotProxyEndpoints");
        VpnHotspotReconnectHint = L("VpnHotspotReconnectHint");
        VpnHotspotIcsNote = L("VpnHotspotIcsNote");
        OpenWindowsHotspotLabel = L("VpnHotspotOpenWindowsHotspot");
        OpenNetworkAdaptersLabel = L("VpnHotspotOpenAdapters");
        CopyHotspotProxyLabel = L("VpnHotspotCopyProxy");
    }

    internal void RefreshVpnHotspotPanel()
    {
        var connected = _vpn.State == ConnectionState.Connected;
        VpnHotspotVpnStatus = connected ? L("VpnHotspotVpnOn") : L("VpnHotspotVpnOff");
        VpnHotspotProxyStatus = AllowLanConnections ? L("VpnHotspotProxyOn") : L("VpnHotspotProxyOff");

        if (!AllowLanConnections)
        {
            VpnHotspotHostIp = "—";
            VpnHotspotProxyLines = L("VpnHotspotEnableLanFirst");
            return;
        }

        var host = WindowsVpnHotspotHelper.DetectShareHostIpv4();
        VpnHotspotHostIp = host;
        VpnHotspotProxyLines =
            $"{F("VpnHotspotSocksLine", host, LocalSocksPort)}\n{F("VpnHotspotHttpLine", host, LocalHttpPort)}";
        LocalLanIp = host;
        OnPropertyChanged(nameof(LocalLanIp));
    }

    [RelayCommand]
    private void OpenWindowsHotspotSettings() => WindowsVpnHotspotHelper.OpenMobileHotspotSettings();

    [RelayCommand]
    private void OpenNetworkAdapters() => WindowsVpnHotspotHelper.OpenNetworkConnections();

    [RelayCommand]
    private void CopyVpnHotspotProxy()
    {
        if (!AllowLanConnections)
        {
            UpdateMessage = L("VpnHotspotEnableLanFirst");
            return;
        }

        var host = WindowsVpnHotspotHelper.DetectShareHostIpv4();
        var text = WindowsVpnHotspotHelper.BuildCopyBlock(host, LocalSocksPort, LocalHttpPort);
        try
        {
            Clipboard.SetText(text);
            UpdateMessage = L("VpnHotspotCopied");
        }
        catch
        {
            UpdateMessage = L("VpnHotspotCopyFailed");
        }
    }
}