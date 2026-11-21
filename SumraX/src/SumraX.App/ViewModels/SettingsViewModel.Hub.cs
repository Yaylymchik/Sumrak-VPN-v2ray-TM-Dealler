using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SumraX.App.ViewModels;

public partial class SettingsViewModel
{
    public ObservableCollection<SettingsHubRowViewModel> HubUiRows { get; } = [];
    public ObservableCollection<SettingsHubRowViewModel> HubTunnelBeforeRows { get; } = [];
    public ObservableCollection<SettingsHubRowViewModel> HubTunnelAfterRows { get; } = [];
    public ObservableCollection<SettingsHubRowViewModel> HubAdvancedBeforeRows { get; } = [];
    public ObservableCollection<SettingsHubRowViewModel> HubAdvancedAfterRows { get; } = [];
    public ObservableCollection<SettingsHubRowViewModel> HubOtherRows { get; } = [];
    public ObservableCollection<SettingsHubRowViewModel> HubAboutRows { get; } = [];

    [ObservableProperty] private bool _sendHwidHeader = true;
    [ObservableProperty] private string _customHwidValue = string.Empty;
    public string SendHwidHeaderLabel { get; private set; } = "";
    public string SendHwidHeaderSummary { get; private set; } = "";
    public string CustomHwidValueLabel { get; private set; } = "";
    public string CustomHwidValueSummary { get; private set; } = "";
    public string XrayTunEnabledLabel { get; private set; } = "";
    public string UrlSchemesInfo { get; private set; } = "";

    private const string FaqUrl = "https://github.com/Yaylymchik/Sumrak-VPN-v2ray-TM-Dealler";

    private void RebuildHubRows()
    {
        static void Fill(ObservableCollection<SettingsHubRowViewModel> target, IEnumerable<SettingsHubRowViewModel> rows)
        {
            target.Clear();
            foreach (var row in rows)
            {
                target.Add(row);
            }
        }

        Fill(HubUiRows,
        [
            Row(L("SettingsRowLanguage"), SettingsSection.General),
            Row(L("SettingsRowUiMode"), SettingsSection.General),
            Row(L("SettingsRowUiSettings"), SettingsSection.General),
        ]);

        Fill(HubTunnelBeforeRows,
        [
            Row(L("SettingsRowRouting"), SettingsSection.Routing),
            Row(L("SettingsRowPerAppProxy"), SettingsSection.Routing),
            Row(L("SettingsRowInbound"), SettingsSection.Network),
        ]);

        Fill(HubTunnelAfterRows,
        [
            Row(L("SettingsRowTunnelOptions"), SettingsSection.Tunnel),
            Row(L("SettingsRowVpnHotspot"), SettingsSection.VpnHotspot),
        ]);

        Fill(HubAdvancedBeforeRows,
        [
            Row(L("SettingsRowVpnSettings"), SettingsSection.Connection),
            Row(L("SettingsRowSubscriptions"), SettingsSection.Subscriptions),
            Row(L("SettingsRowPing"), SettingsSection.Ping),
        ]);

        Fill(HubAdvancedAfterRows,
        [
            Row(L("SettingsRowLan"), SettingsSection.Network),
            Row(L("SettingsRowAutoStart"), SettingsSection.Connection),
            Row(L("SettingsRowAdvancedMisc"), SettingsSection.Advanced),
            Row(L("SettingsRowCoreDns"), SettingsSection.Core),
        ]);

        Fill(HubOtherRows,
        [
            Row(L("SettingsRowCheckUpdate"), SettingsSection.Diagnostics, action: "CheckUpdate"),
            Row(L("SettingsRowStatistics"), SettingsSection.Diagnostics, action: "Statistics"),
            Row(L("SettingsRowLogs"), SettingsSection.Logs),
            Row(L("SettingsRowOther"), SettingsSection.Data),
            Row(L("SettingsRowReset"), SettingsSection.Data, action: "Reset", isDanger: true),
        ]);

        Fill(HubAboutRows,
        [
            Row(L("SettingsRowFaq"), action: "Faq"),
            Row(L("SettingsRowUrlSchemes"), action: "UrlSchemes"),
            Row(L("SettingsRowAbout"), SettingsSection.Data),
        ]);
    }

    private static SettingsHubRowViewModel Row(
        string title,
        SettingsSection section = SettingsSection.Root,
        string? action = null,
        bool isDanger = false) => new()
    {
        Title = title,
        Section = section,
        Action = action,
        IsDanger = isDanger,
    };

    [RelayCommand]
    private void OpenHubRow(SettingsHubRowViewModel? row)
    {
        if (row == null)
        {
            return;
        }

        switch (row.Action)
        {
            case "Faq":
                TryOpenUrl(FaqUrl);
                return;
            case "UrlSchemes":
                UpdateMessage = UrlSchemesInfo;
                return;
            case "CheckUpdate":
            case "Statistics":
                CurrentSection = SettingsSection.Diagnostics;
                return;
            case "Reset":
                CurrentSection = SettingsSection.Data;
                return;
        }

        CurrentSection = row.Section;
        if (row.Section == SettingsSection.Logs)
        {
            _ = RefreshLogsAsync();
        }
    }

    public void NavigateToSubscription(string? subscriptionUrl)
    {
        CurrentSection = SettingsSection.Subscriptions;
        if (string.IsNullOrWhiteSpace(subscriptionUrl))
        {
            return;
        }

        var match = SubscriptionUrls.FirstOrDefault(u =>
            string.Equals(u, subscriptionUrl, StringComparison.OrdinalIgnoreCase));
        if (match != null)
        {
            SelectedSubscriptionUrl = match;
        }
    }

    private static void TryOpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }
}