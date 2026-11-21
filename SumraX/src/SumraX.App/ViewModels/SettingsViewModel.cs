using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using SumraX.Core;
using SumraX.Core.Enums;
using SumraX.App.Markup;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Network;
using SumraX.Infrastructure.Security;
using SumraX.Infrastructure.Vpn;

namespace SumraX.App.ViewModels;

public sealed class DiagnosticRowViewModel
{
    public string Name { get; init; } = "";
    public string Detail { get; init; } = "";
    public string StatusGlyph { get; init; } = "•";
}

public sealed class SubscriptionTrafficRowViewModel
{
    public string Url { get; init; } = "";
    public string TrafficText { get; init; } = "";
    public string ExpireText { get; init; } = "";
}

public sealed class NamedOption
{
    public string Value { get; init; } = "";
    public string Display { get; init; } = "";
    public override string ToString() => Display;
}

public partial class SettingsViewModel : LocalizableViewModelBase
{
    private readonly ISettingsRepository _settings;
    private readonly IAutoStartService _autoStart;
    private readonly IKillSwitchService _killSwitch;
    private readonly IBackupService _backup;
    private readonly ISubscriptionRefreshService _subRefresh;
    private readonly IDiagnosticsService _diagnostics;
    private readonly IDebugExportService _debugExport;
    private readonly IConflictDetectionService _conflicts;
    private readonly ILogViewerService _logViewer;
    // IVpnEngine field lives in SettingsViewModel.VpnHotspot.cs
    private bool _suppressPersist;
    private CancellationTokenSource? _autoSaveCts;

    public int[] RefreshIntervalOptions { get; } = [10, 20, 30, 60, 120, 360, 720, 1440];

    [ObservableProperty] private SettingsSection _currentSection = SettingsSection.Root;
    [ObservableProperty] private bool _autoStartEnabled;
    [ObservableProperty] private bool _killSwitchEnabled;
    [ObservableProperty] private bool _dnsLeakProtection = true;
    [ObservableProperty] private bool _autoReconnect = true;
    [ObservableProperty] private bool _autoSelectFastest;
    [ObservableProperty] private bool _autoRefreshSubscriptions = true;
    [ObservableProperty] private bool _updateSubscriptionsOnStart = true;
    [ObservableProperty] private bool _connectOnStart;
    [ObservableProperty] private bool _pingOnStart = true;
    [ObservableProperty] private PingType _selectedPingType = PingType.Proxy;
    [ObservableProperty] private bool _allowLanConnections;
    [ObservableProperty] private bool _bypassLocalForSystemProxy = true;
    [ObservableProperty] private bool _useSystemProxy = true;
    [ObservableProperty] private bool _useLocalDns;
    [ObservableProperty] private bool _useDnsFromJson;
    [ObservableProperty] private bool _enableServerResolving;
    [ObservableProperty] private bool _ignoreDuplicateServers = true;
    [ObservableProperty] private bool _purgeOldProfilesOnSubscriptionUpdate = true;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _useTunMode = true;
    [ObservableProperty] private int _tunMtu = 1500;
    [ObservableProperty] private TunProvider _selectedTunProvider = TunProvider.Tun2Proxy;
    [ObservableProperty] private TunStackMode _selectedTunStackMode = TunStackMode.Mixed;
    [ObservableProperty] private bool _tunDnsEnable = true;
    [ObservableProperty] private bool _sniffTraffic;
    [ObservableProperty] private bool _routeOnly;
    [ObservableProperty] private bool _ipv6Enabled;
    [ObservableProperty] private bool _preferIpv6;
    [ObservableProperty] private bool _fakeDnsEnabled;
    [ObservableProperty] private string _domesticDns = "8.8.8.8";
    [ObservableProperty] private string _dnsHosts = string.Empty;
    [ObservableProperty] private string _coreLogLevel = "warning";
    [ObservableProperty] private bool _socksEnableUdp = true;
    [ObservableProperty] private bool _muxEnabled;
    [ObservableProperty] private int _muxConcurrency = 8;
    [ObservableProperty] private int _muxXudpConcurrency = 8;
    [ObservableProperty] private string _muxXudpQuic = "reject";
    [ObservableProperty] private bool _fragmentEnabled;
    [ObservableProperty] private string _fragmentPackets = "tlshello";
    [ObservableProperty] private string _fragmentLength = "50-100";
    [ObservableProperty] private string _fragmentInterval = "10-20";
    [ObservableProperty] private string _fragmentMaxSplit = "10-20";
    [ObservableProperty] private bool _noiseEnabled;
    [ObservableProperty] private string _noiseRand = "10-20";
    [ObservableProperty] private string _noiseDelay = "10-16";
    [ObservableProperty] private bool _autoSortAfterTest = true;
    [ObservableProperty] private bool _autoRemoveInvalidAfterTest;
    [ObservableProperty] private string _delayTestUrl = "https://www.gstatic.com/generate_204";
    [ObservableProperty] private string _ipApiUrl = "https://api.ip.sb/geoip";
    [ObservableProperty] private int _realPingConcurrency = 16;
    [ObservableProperty] private bool _speedDisplayEnabled = true;
    [ObservableProperty] private bool _confirmRemove;
    [ObservableProperty] private bool _checkConflictsOnStart = true;
    [ObservableProperty] private bool _routingEnabled;
    [ObservableProperty] private ClientUserAgentPreset _selectedUserAgent = ClientUserAgentPreset.ChromeWindows;
    [ObservableProperty] private RoutingMode _selectedRoutingMode = RoutingMode.BypassPrivate;
    [ObservableProperty] private string _domainDirectRules = string.Empty;
    [ObservableProperty] private string _splitTunnelProcesses = string.Empty;
    [ObservableProperty] private string _logText = string.Empty;
    [ObservableProperty] private bool _isRunningDiagnostics;
    [ObservableProperty] private int _subscriptionRefreshIntervalMinutes = 20;
    [ObservableProperty] private int _localSocksPort = 10808;
    [ObservableProperty] private int _localHttpPort = 10809;
    [ObservableProperty] private string _dnsServer = "1.1.1.1";
    [ObservableProperty] private string _remoteDns = "https://cloudflare-dns.com/dns-query";
    [ObservableProperty] private string _newSubscriptionUrl = string.Empty;
    [ObservableProperty] private string? _selectedSubscriptionUrl;
    [ObservableProperty] private string _localLanIp = "—";
    [ObservableProperty] private AppLanguage _selectedLanguage = AppLanguage.Russian;
    [ObservableProperty] private AppTheme _selectedTheme = AppTheme.Cyber;
    [ObservableProperty] private string _updateMessage = string.Empty;
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _sectionTitle = "";

    public ObservableCollection<string> SubscriptionUrls { get; } = [];
    public ObservableCollection<DiagnosticRowViewModel> DiagnosticRows { get; } = [];
    public ObservableCollection<SubscriptionTrafficRowViewModel> SubscriptionTrafficRows { get; } = [];
    public ObservableCollection<DiagnosticRowViewModel> ConflictRows { get; } = [];
    public Array RoutingModes => Enum.GetValues(typeof(RoutingMode));
    public ClientUserAgentPreset[] UserAgentPresets => ClientUserAgentProfiles.AllPresets;
    public Array TunStackModes => Enum.GetValues(typeof(TunStackMode));
    public Array TunProviders => Enum.GetValues(typeof(TunProvider));
    public Array PingTypes => Enum.GetValues(typeof(PingType));
    public bool IsSingBoxTunProvider => SelectedTunProvider != TunProvider.Tun2Proxy;

    public bool IsRootSection => CurrentSection == SettingsSection.Root;
    public bool IsDetailSection => !IsRootSection;
    public Array Languages => Enum.GetValues(typeof(AppLanguage));
    public Array ThemeOptions => Enum.GetValues(typeof(AppTheme));

    public string SectionGeneralLabel { get; private set; } = "";
    public string SectionAdvancedLabel { get; private set; } = "";
    public string SectionCoreLabel { get; private set; } = "";
    public string SectionPingLabel { get; private set; } = "";
    public string SectionOtherLabel { get; private set; } = "";
    public string SectionSubscriptionsLabel { get; private set; } = "";
    public string SectionConnectionLabel { get; private set; } = "";
    public string SectionNetworkLabel { get; private set; } = "";
    public string SectionDataLabel { get; private set; } = "";
    public string SectionDiagnosticsLabel { get; private set; } = "";
    public string SectionTunnelLabel { get; private set; } = "";
    public string SectionRoutingLabel { get; private set; } = "";
    public string SectionLogsLabel { get; private set; } = "";
    public string HubUiLabel { get; private set; } = "";
    public string HubTunnelLabel { get; private set; } = "";
    public string HubAdvancedLabel { get; private set; } = "";
    public string HubOtherLabel { get; private set; } = "";
    public string HubAboutLabel { get; private set; } = "";
    public string BackLabel { get; private set; } = "";
    public string AutoStartLabel { get; private set; } = "";
    public string KillSwitchLabel { get; private set; } = "";
    public string DnsLeakLabel { get; private set; } = "";
    public string AutoReconnectLabel { get; private set; } = "";
    public string AutoSelectFastestLabel { get; private set; } = "";
    public string AutoRefreshSubscriptionsLabel { get; private set; } = "";
    public string UpdateSubscriptionsOnStartLabel { get; private set; } = "";
    public string ConnectOnStartLabel { get; private set; } = "";
    public string PingOnStartLabel { get; private set; } = "";
    public string PingTypeLabel { get; private set; } = "";
    public string IgnoreDuplicatesLabel { get; private set; } = "";
    public string PurgeOldProfilesLabel { get; private set; } = "";
    public string SubscriptionRefreshIntervalLabel { get; private set; } = "";
    public string RefreshSubscriptionsLabel { get; private set; } = "";
    public string AddSubscriptionLabel { get; private set; } = "";
    public string RemoveSubscriptionLabel { get; private set; } = "";
    public string SubscriptionUrlsLabel { get; private set; } = "";
    public string LanguageLabel { get; private set; } = "";
    public string ThemeLabel { get; private set; } = "";
    public string DnsServerLabel { get; private set; } = "";
    public string RemoteDnsLabel { get; private set; } = "";
    public string LocalSocksPortLabel { get; private set; } = "";
    public string LocalHttpPortLabel { get; private set; } = "";
    public string AllowLanLabel { get; private set; } = "";
    public string BypassLocalLabel { get; private set; } = "";
    public string UseSystemProxyLabel { get; private set; } = "";
    public string UseLocalDnsLabel { get; private set; } = "";
    public string UseDnsFromJsonLabel { get; private set; } = "";
    public string EnableServerResolvingLabel { get; private set; } = "";
    public string ExcludedRoutesLabel { get; private set; } = "";
    public string LanInfoLabel { get; private set; } = "";
    public string SaveLabel { get; private set; } = "";
    public string BackupLabel { get; private set; } = "";
    public string RestoreLabel { get; private set; } = "";
    public string KillSwitchDesc { get; private set; } = "";
    public string DnsLeakDesc { get; private set; } = "";
    public string AutoReconnectDesc { get; private set; } = "";
    public string AutoStartDesc { get; private set; } = "";
    public string AutoSelectFastestDesc { get; private set; } = "";
    public string AutoRefreshSubsDesc { get; private set; } = "";
    public string UpdateOnStartDesc { get; private set; } = "";
    public string ConnectOnStartDesc { get; private set; } = "";
    public string PingOnStartDesc { get; private set; } = "";
    public string IgnoreDuplicatesDesc { get; private set; } = "";
    public string AllowLanDesc { get; private set; } = "";
    public string BypassLocalDesc { get; private set; } = "";
    public string NetworkPortsDesc { get; private set; } = "";
    public string ProxyModeNotice { get; private set; } = "";
    public string MinimizeToTrayLabel { get; private set; } = "";
    public string MinimizeToTrayDesc { get; private set; } = "";
    public string RunDiagnosticsLabel { get; private set; } = "";
    public string ExportDebugLabel { get; private set; } = "";
    public string SubscriptionTrafficLabel { get; private set; } = "";
    public string DiagnosticsDesc { get; private set; } = "";
    public string AppVersionLabel { get; private set; } = "";
    public string UseTunModeLabel { get; private set; } = "";
    public string UseTunModeDesc { get; private set; } = "";
    public string TunMtuLabel { get; private set; } = "";
    public string TunStackModeLabel { get; private set; } = "";
    public string TunProviderLabel { get; private set; } = "";
    public string TunDnsEnableLabel { get; private set; } = "";
    public string TunDnsEnableDesc { get; private set; } = "";
    public string TunDnsAddressLabel { get; private set; } = "";
    public string SniffTrafficLabel { get; private set; } = "";
    public string SniffTrafficDesc { get; private set; } = "";
    public string CheckConflictsLabel { get; private set; } = "";
    public string CheckConflictsDesc { get; private set; } = "";
    public string RoutingModeLabel { get; private set; } = "";
    public string RoutingEnabledLabel { get; private set; } = "";
    public string SubscriptionUserAgentLabel { get; private set; } = "";
    public string RoutingRulesDesc { get; private set; } = "";
    public string DomainDirectRulesLabel { get; private set; } = "";
    public string DomainDirectRulesDesc { get; private set; } = "";
    public string SplitTunnelLabel { get; private set; } = "";
    public string SplitTunnelDesc { get; private set; } = "";
    public string RefreshLogsLabel { get; private set; } = "";
    public string LogsDesc { get; private set; } = "";
    public string RunConflictCheckLabel { get; private set; } = "";
    public string TunModeNotice { get; private set; } = "";
    public string MuxEnabledLabel { get; private set; } = "";
    public string MuxConcurrencyLabel { get; private set; } = "";
    public string MuxXudpConcurrencyLabel { get; private set; } = "";
    public string MuxXudpQuicLabel { get; private set; } = "";
    public string FragmentEnabledLabel { get; private set; } = "";
    public string FragmentPacketsLabel { get; private set; } = "";
    public string FragmentLengthLabel { get; private set; } = "";
    public string FragmentIntervalLabel { get; private set; } = "";
    public string FragmentMaxSplitLabel { get; private set; } = "";
    public string FragmentCoreTypeLabel { get; private set; } = "";
    public string AppProxySettingsLabel { get; private set; } = "";
    public string AppProxySettingsHint { get; private set; } = "";
    public string NoiseEnabledLabel { get; private set; } = "";
    public string NoiseRandLabel { get; private set; } = "";
    public string NoiseDelayLabel { get; private set; } = "";
    public string Ipv6EnabledLabel { get; private set; } = "";
    public string PreferIpv6Label { get; private set; } = "";
    public string PreferredIpTypeLabel { get; private set; } = "";
    public string InboundsLabel { get; private set; } = "";
    public string InboundsStatusLabel { get; private set; } = "";
    public string FakeDnsEnabledLabel { get; private set; } = "";
    public string RouteOnlyLabel { get; private set; } = "";
    public string SocksEnableUdpLabel { get; private set; } = "";
    public string DomesticDnsLabel { get; private set; } = "";
    public string DnsHostsLabel { get; private set; } = "";
    public string CoreLogLevelLabel { get; private set; } = "";
    public string AutoSortAfterTestLabel { get; private set; } = "";
    public string AutoRemoveInvalidAfterTestLabel { get; private set; } = "";
    public string SpeedDisplayEnabledLabel { get; private set; } = "";
    public string ConfirmRemoveLabel { get; private set; } = "";
    public string DelayTestUrlLabel { get; private set; } = "";
    public string IpApiUrlLabel { get; private set; } = "";
    public string RealPingConcurrencyLabel { get; private set; } = "";

    public string[] CoreLogLevels { get; } = ["debug", "info", "warning", "error", "none"];
    public string[] FragmentPacketOptions { get; } = ["tlshello", "1-1", "1-2", "1-3", "1-5"];
    public string[] FragmentEngineOptions { get; private set; } = ["Xray", "Advanced"];
    public ObservableCollection<NamedOption> MuxXudpQuicChoices { get; } = [];
    public string[] PreferredIpTypeOptions { get; } = ["IPv4", "IPv6", "Auto"];
    [ObservableProperty] private string _selectedPreferredIpType = "IPv4";
    [ObservableProperty] private string _fragmentEngine = "Xray";
    [ObservableProperty] private NamedOption? _selectedMuxXudpQuic;

    public bool IsFragmentAdvanced =>
        string.Equals(FragmentEngine, "Advanced", StringComparison.OrdinalIgnoreCase);

    public SettingsViewModel(
        ISettingsRepository settings,
        IAutoStartService autoStart,
        IKillSwitchService killSwitch,
        IBackupService backup,
        ISubscriptionRefreshService subRefresh,
        IDiagnosticsService diagnostics,
        IDebugExportService debugExport,
        IConflictDetectionService conflicts,
        ILogViewerService logViewer,
        IVpnEngine vpn,
        ILocalizationService loc) : base(loc)
    {
        _vpn = vpn;
        _settings = settings;
        _autoStart = autoStart;
        _killSwitch = killSwitch;
        _backup = backup;
        _subRefresh = subRefresh;
        _diagnostics = diagnostics;
        _debugExport = debugExport;
        _conflicts = conflicts;
        _logViewer = logViewer;
        _vpn.StatusChanged += (_, _) =>
        {
            if (CurrentSection == SettingsSection.VpnHotspot)
            {
                RefreshVpnHotspotPanel();
            }
        };
        CompleteInitialization();
        _ = LoadAsync();
    }

    partial void OnCurrentSectionChanged(SettingsSection value)
    {
        OnPropertyChanged(nameof(IsRootSection));
        OnPropertyChanged(nameof(IsDetailSection));
        UpdateSectionTitle();
        if (value == SettingsSection.VpnHotspot)
        {
            RefreshVpnHotspotPanel();
        }
    }

    protected override void OnLanguageChanged() => ApplyLabels();

    private void ApplyLabels()
    {
        PageTitle = L("NavSettings");
        SectionGeneralLabel = L("SettingsSectionGeneral");
        SectionAdvancedLabel = L("SettingsSectionAdvanced");
        SectionCoreLabel = L("SettingsSectionCore");
        SectionPingLabel = L("SettingsSectionPing");
        SectionOtherLabel = L("SettingsSectionOther");
        SectionSubscriptionsLabel = L("SettingsSectionSubscriptions");
        SectionConnectionLabel = L("SettingsSectionConnection");
        SectionNetworkLabel = L("SettingsSectionNetwork");
        SectionDataLabel = L("SettingsSectionData");
        SectionDiagnosticsLabel = L("SettingsSectionDiagnostics");
        SectionTunnelLabel = L("SettingsSectionTunnel");
        SectionRoutingLabel = L("SettingsSectionRouting");
        SectionLogsLabel = L("SettingsSectionLogs");
        HubUiLabel = L("SettingsHubUi");
        HubTunnelLabel = L("SettingsHubTunnel");
        HubAdvancedLabel = L("SettingsHubAdvanced");
        HubOtherLabel = L("SettingsHubOther");
        HubAboutLabel = L("SettingsHubAbout");
        BackLabel = L("SettingsBack");
        AutoStartLabel = L("AutoStartWindows");
        KillSwitchLabel = L("KillSwitch");
        DnsLeakLabel = L("DnsLeakProtection");
        AutoReconnectLabel = L("AutoReconnect");
        AutoSelectFastestLabel = L("AutoSelectFastest");
        AutoRefreshSubscriptionsLabel = L("AutoRefreshSubscriptions");
        UpdateSubscriptionsOnStartLabel = L("UpdateSubscriptionsOnStart");
        ConnectOnStartLabel = L("ConnectOnStart");
        PingOnStartLabel = L("PingOnStart");
        PingTypeLabel = L("PingType");
        IgnoreDuplicatesLabel = L("IgnoreDuplicateServers");
        PurgeOldProfilesLabel = L("PurgeOldProfilesOnSubscriptionUpdate");
        SubscriptionRefreshIntervalLabel = L("SubscriptionRefreshInterval");
        RefreshSubscriptionsLabel = L("RefreshSubscriptionsNow");
        AddSubscriptionLabel = L("AddSubscriptionUrl");
        RemoveSubscriptionLabel = L("RemoveSubscriptionUrl");
        SubscriptionUrlsLabel = L("SubscriptionUrlsList");
        LanguageLabel = L("Language");
        ThemeLabel = L("Theme");
        DnsServerLabel = L("DnsServer");
        RemoteDnsLabel = L("RemoteDns");
        LocalSocksPortLabel = L("LocalSocksPort");
        LocalHttpPortLabel = L("LocalHttpPort");
        AllowLanLabel = L("AllowLanConnections");
        BypassLocalLabel = L("BypassLocalProxy");
        UseSystemProxyLabel = L("UseSystemProxy");
        UseLocalDnsLabel = L("UseLocalDns");
        UseDnsFromJsonLabel = L("UseDnsFromJson");
        EnableServerResolvingLabel = L("EnableServerResolving");
        ExcludedRoutesLabel = L("ExcludedRoutes");
        LanInfoLabel = L("LanConnectionInfo");
        SaveLabel = L("Save");
        BackupLabel = L("Backup");
        RestoreLabel = L("Restore");
        KillSwitchDesc = L("KillSwitchDesc");
        DnsLeakDesc = L("DnsLeakDesc");
        AutoReconnectDesc = L("AutoReconnectDesc");
        AutoStartDesc = L("AutoStartDesc");
        AutoSelectFastestDesc = L("AutoSelectFastestDesc");
        AutoRefreshSubsDesc = L("AutoRefreshSubsDesc");
        UpdateOnStartDesc = L("UpdateOnStartDesc");
        ConnectOnStartDesc = L("ConnectOnStartDesc");
        PingOnStartDesc = L("PingOnStartDesc");
        IgnoreDuplicatesDesc = L("IgnoreDuplicatesDesc");
        AllowLanDesc = L("AllowLanDesc");
        BypassLocalDesc = L("BypassLocalDesc");
        NetworkPortsDesc = L("NetworkPortsDesc");
        ProxyModeNotice = L("ProxyModeNotice");
        MinimizeToTrayLabel = L("MinimizeToTray");
        MinimizeToTrayDesc = L("MinimizeToTrayDesc");
        RunDiagnosticsLabel = L("RunDiagnostics");
        ExportDebugLabel = L("ExportDebugBundle");
        SubscriptionTrafficLabel = L("SubscriptionTraffic");
        DiagnosticsDesc = L("DiagnosticsDesc");
        AppVersionLabel = $"v{AppInfo.Version} · Sumrak VPN";
        UseTunModeLabel = L("UseTunMode");
        UseTunModeDesc = L("UseTunModeDesc");
        TunMtuLabel = L("TunMtu");
        TunStackModeLabel = L("TunStackMode");
        TunProviderLabel = L("TunProvider");
        TunDnsEnableLabel = L("TunDnsEnable");
        TunDnsEnableDesc = L("TunDnsEnableDesc");
        TunDnsAddressLabel = L("TunDnsAddress");
        SniffTrafficLabel = L("SniffTraffic");
        SniffTrafficDesc = L("SniffTrafficDesc");
        CheckConflictsLabel = L("CheckConflictsOnStart");
        CheckConflictsDesc = L("CheckConflictsOnStartDesc");
        RoutingModeLabel = L("RoutingMode");
        RoutingEnabledLabel = L("RoutingEnabled");
        SubscriptionUserAgentLabel = L("SubscriptionUserAgent");
        RoutingRulesDesc = L("RoutingRulesDesc");
        DomainDirectRulesLabel = L("DomainDirectRules");
        DomainDirectRulesDesc = L("DomainDirectRulesDesc");
        SplitTunnelLabel = L("SplitTunnelProcesses");
        SplitTunnelDesc = L("SplitTunnelDesc");
        RefreshLogsLabel = L("RefreshLogs");
        LogsDesc = L("LogsDesc");
        RunConflictCheckLabel = L("RunConflictCheck");
        TunModeNotice = L("TunModeNotice");
        MuxEnabledLabel = L("MuxEnabled");
        MuxConcurrencyLabel = L("MuxConcurrency");
        MuxXudpConcurrencyLabel = L("MuxXudpConcurrency");
        MuxXudpQuicLabel = L("MuxXudpQuic");
        FragmentEnabledLabel = L("FragmentEnabled");
        FragmentPacketsLabel = L("FragmentPackets");
        FragmentLengthLabel = L("FragmentLength");
        FragmentIntervalLabel = L("FragmentInterval");
        FragmentMaxSplitLabel = L("FragmentMaxSplit");
        FragmentCoreTypeLabel = L("FragmentCoreType");
        AppProxySettingsLabel = L("AppProxySettings");
        AppProxySettingsHint = L("AppProxySettingsHint");
        NoiseEnabledLabel = L("NoiseEnabled");
        NoiseRandLabel = L("NoiseRand");
        NoiseDelayLabel = L("NoiseDelay");
        Ipv6EnabledLabel = L("Ipv6Enabled");
        PreferIpv6Label = L("PreferIpv6");
        PreferredIpTypeLabel = L("PreferredIpType");
        InboundsLabel = L("Inbounds");
        RefreshMuxQuicChoices();
        RefreshInboundsStatus();
        FakeDnsEnabledLabel = L("FakeDnsEnabled");
        RouteOnlyLabel = L("RouteOnly");
        SocksEnableUdpLabel = L("SocksEnableUdp");
        DomesticDnsLabel = L("DomesticDns");
        DnsHostsLabel = L("DnsHosts");
        CoreLogLevelLabel = L("CoreLogLevel");
        AutoSortAfterTestLabel = L("AutoSortAfterTest");
        AutoRemoveInvalidAfterTestLabel = L("AutoRemoveInvalidAfterTest");
        SpeedDisplayEnabledLabel = L("SpeedDisplayEnabled");
        ConfirmRemoveLabel = L("ConfirmRemove");
        DelayTestUrlLabel = L("DelayTestUrl");
        IpApiUrlLabel = L("IpApiUrl");
        RealPingConcurrencyLabel = L("RealPingConcurrency");
        SendHwidHeaderLabel = L("SendHwidHeader");
        SendHwidHeaderSummary = L("SendHwidHeaderSummary");
        CustomHwidValueLabel = L("CustomHwidValue");
        CustomHwidValueSummary = L("CustomHwidValueSummary");
        XrayTunEnabledLabel = L("XrayTunEnabled");
        UrlSchemesInfo = L("UrlSchemesInfo");
        ApplyVpnHotspotLabels();
        RebuildHubRows();
        UpdateSectionTitle();
        OnPropertyChanged(nameof(SectionGeneralLabel));
        OnPropertyChanged(nameof(SectionAdvancedLabel));
        OnPropertyChanged(nameof(SectionCoreLabel));
        OnPropertyChanged(nameof(SectionPingLabel));
        OnPropertyChanged(nameof(SectionOtherLabel));
        OnPropertyChanged(nameof(SectionSubscriptionsLabel));
        OnPropertyChanged(nameof(SectionConnectionLabel));
        OnPropertyChanged(nameof(SectionNetworkLabel));
        OnPropertyChanged(nameof(SectionDataLabel));
        OnPropertyChanged(nameof(SectionDiagnosticsLabel));
        OnPropertyChanged(nameof(SectionTunnelLabel));
        OnPropertyChanged(nameof(SectionRoutingLabel));
        OnPropertyChanged(nameof(SectionLogsLabel));
        OnPropertyChanged(nameof(HubUiLabel));
        OnPropertyChanged(nameof(HubTunnelLabel));
        OnPropertyChanged(nameof(HubAdvancedLabel));
        OnPropertyChanged(nameof(HubOtherLabel));
        OnPropertyChanged(nameof(HubAboutLabel));
        OnPropertyChanged(nameof(BackLabel));
        OnPropertyChanged(nameof(AutoStartLabel));
        OnPropertyChanged(nameof(KillSwitchLabel));
        OnPropertyChanged(nameof(DnsLeakLabel));
        OnPropertyChanged(nameof(AutoReconnectLabel));
        OnPropertyChanged(nameof(AutoSelectFastestLabel));
        OnPropertyChanged(nameof(AutoRefreshSubscriptionsLabel));
        OnPropertyChanged(nameof(UpdateSubscriptionsOnStartLabel));
        OnPropertyChanged(nameof(ConnectOnStartLabel));
        OnPropertyChanged(nameof(PingOnStartLabel));
        OnPropertyChanged(nameof(PingTypeLabel));
        OnPropertyChanged(nameof(IgnoreDuplicatesLabel));
        OnPropertyChanged(nameof(PurgeOldProfilesLabel));
        OnPropertyChanged(nameof(SubscriptionRefreshIntervalLabel));
        OnPropertyChanged(nameof(RefreshSubscriptionsLabel));
        OnPropertyChanged(nameof(AddSubscriptionLabel));
        OnPropertyChanged(nameof(RemoveSubscriptionLabel));
        OnPropertyChanged(nameof(SubscriptionUrlsLabel));
        OnPropertyChanged(nameof(LanguageLabel));
        OnPropertyChanged(nameof(ThemeLabel));
        OnPropertyChanged(nameof(DnsServerLabel));
        OnPropertyChanged(nameof(RemoteDnsLabel));
        OnPropertyChanged(nameof(LocalSocksPortLabel));
        OnPropertyChanged(nameof(LocalHttpPortLabel));
        OnPropertyChanged(nameof(AllowLanLabel));
        OnPropertyChanged(nameof(BypassLocalLabel));
        OnPropertyChanged(nameof(UseSystemProxyLabel));
        OnPropertyChanged(nameof(UseLocalDnsLabel));
        OnPropertyChanged(nameof(UseDnsFromJsonLabel));
        OnPropertyChanged(nameof(EnableServerResolvingLabel));
        OnPropertyChanged(nameof(ExcludedRoutesLabel));
        OnPropertyChanged(nameof(LanInfoLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(BackupLabel));
        OnPropertyChanged(nameof(RestoreLabel));
        OnPropertyChanged(nameof(KillSwitchDesc));
        OnPropertyChanged(nameof(DnsLeakDesc));
        OnPropertyChanged(nameof(AutoReconnectDesc));
        OnPropertyChanged(nameof(AutoStartDesc));
        OnPropertyChanged(nameof(AutoSelectFastestDesc));
        OnPropertyChanged(nameof(AutoRefreshSubsDesc));
        OnPropertyChanged(nameof(UpdateOnStartDesc));
        OnPropertyChanged(nameof(ConnectOnStartDesc));
        OnPropertyChanged(nameof(PingOnStartDesc));
        OnPropertyChanged(nameof(IgnoreDuplicatesDesc));
        OnPropertyChanged(nameof(AllowLanDesc));
        OnPropertyChanged(nameof(BypassLocalDesc));
        OnPropertyChanged(nameof(NetworkPortsDesc));
        OnPropertyChanged(nameof(ProxyModeNotice));
        OnPropertyChanged(nameof(MinimizeToTrayLabel));
        OnPropertyChanged(nameof(MinimizeToTrayDesc));
        OnPropertyChanged(nameof(RunDiagnosticsLabel));
        OnPropertyChanged(nameof(ExportDebugLabel));
        OnPropertyChanged(nameof(SubscriptionTrafficLabel));
        OnPropertyChanged(nameof(DiagnosticsDesc));
        OnPropertyChanged(nameof(AppVersionLabel));
        OnPropertyChanged(nameof(UseTunModeLabel));
        OnPropertyChanged(nameof(UseTunModeDesc));
        OnPropertyChanged(nameof(TunMtuLabel));
        OnPropertyChanged(nameof(TunStackModeLabel));
        OnPropertyChanged(nameof(TunProviderLabel));
        OnPropertyChanged(nameof(TunDnsEnableLabel));
        OnPropertyChanged(nameof(TunDnsEnableDesc));
        OnPropertyChanged(nameof(TunDnsAddressLabel));
        OnPropertyChanged(nameof(SniffTrafficLabel));
        OnPropertyChanged(nameof(SniffTrafficDesc));
        OnPropertyChanged(nameof(CheckConflictsLabel));
        OnPropertyChanged(nameof(CheckConflictsDesc));
        OnPropertyChanged(nameof(RoutingModeLabel));
        OnPropertyChanged(nameof(RoutingEnabledLabel));
        OnPropertyChanged(nameof(SubscriptionUserAgentLabel));
        OnPropertyChanged(nameof(RoutingRulesDesc));
        OnPropertyChanged(nameof(DomainDirectRulesLabel));
        OnPropertyChanged(nameof(DomainDirectRulesDesc));
        OnPropertyChanged(nameof(SplitTunnelLabel));
        OnPropertyChanged(nameof(SplitTunnelDesc));
        OnPropertyChanged(nameof(RefreshLogsLabel));
        OnPropertyChanged(nameof(LogsDesc));
        OnPropertyChanged(nameof(RunConflictCheckLabel));
        OnPropertyChanged(nameof(TunModeNotice));
        OnPropertyChanged(nameof(MuxEnabledLabel));
        OnPropertyChanged(nameof(MuxConcurrencyLabel));
        OnPropertyChanged(nameof(MuxXudpConcurrencyLabel));
        OnPropertyChanged(nameof(MuxXudpQuicLabel));
        OnPropertyChanged(nameof(FragmentEnabledLabel));
        OnPropertyChanged(nameof(FragmentPacketsLabel));
        OnPropertyChanged(nameof(FragmentLengthLabel));
        OnPropertyChanged(nameof(FragmentIntervalLabel));
        OnPropertyChanged(nameof(FragmentMaxSplitLabel));
        OnPropertyChanged(nameof(FragmentCoreTypeLabel));
        OnPropertyChanged(nameof(AppProxySettingsLabel));
        OnPropertyChanged(nameof(AppProxySettingsHint));
        OnPropertyChanged(nameof(NoiseEnabledLabel));
        OnPropertyChanged(nameof(NoiseRandLabel));
        OnPropertyChanged(nameof(NoiseDelayLabel));
        OnPropertyChanged(nameof(Ipv6EnabledLabel));
        OnPropertyChanged(nameof(PreferIpv6Label));
        OnPropertyChanged(nameof(PreferredIpTypeLabel));
        OnPropertyChanged(nameof(InboundsLabel));
        OnPropertyChanged(nameof(InboundsStatusLabel));
        OnPropertyChanged(nameof(IsFragmentAdvanced));
        OnPropertyChanged(nameof(FakeDnsEnabledLabel));
        OnPropertyChanged(nameof(RouteOnlyLabel));
        OnPropertyChanged(nameof(SocksEnableUdpLabel));
        OnPropertyChanged(nameof(DomesticDnsLabel));
        OnPropertyChanged(nameof(DnsHostsLabel));
        OnPropertyChanged(nameof(CoreLogLevelLabel));
        OnPropertyChanged(nameof(AutoSortAfterTestLabel));
        OnPropertyChanged(nameof(AutoRemoveInvalidAfterTestLabel));
        OnPropertyChanged(nameof(SpeedDisplayEnabledLabel));
        OnPropertyChanged(nameof(ConfirmRemoveLabel));
        OnPropertyChanged(nameof(DelayTestUrlLabel));
        OnPropertyChanged(nameof(IpApiUrlLabel));
        OnPropertyChanged(nameof(RealPingConcurrencyLabel));
    }

    private void UpdateSectionTitle()
    {
        SectionTitle = CurrentSection switch
        {
            SettingsSection.General => SectionGeneralLabel,
            SettingsSection.Advanced => SectionAdvancedLabel,
            SettingsSection.Core => SectionCoreLabel,
            SettingsSection.Ping => SectionPingLabel,
            SettingsSection.Subscriptions => SectionSubscriptionsLabel,
            SettingsSection.Connection => SectionConnectionLabel,
            SettingsSection.Network => SectionNetworkLabel,
            SettingsSection.Data => SectionDataLabel,
            SettingsSection.Diagnostics => SectionDiagnosticsLabel,
            SettingsSection.Tunnel => SectionTunnelLabel,
            SettingsSection.Routing => SectionRoutingLabel,
            SettingsSection.Logs => SectionLogsLabel,
            SettingsSection.VpnHotspot => SectionVpnHotspotLabel,
            _ => PageTitle
        };
        OnPropertyChanged(nameof(SectionTitle));
    }

    partial void OnSelectedTunProviderChanged(TunProvider value) =>
        OnPropertyChanged(nameof(IsSingBoxTunProvider));

    [RelayCommand]
    private void OpenSection(string section)
    {
        if (Enum.TryParse<SettingsSection>(section, out var parsed))
        {
            CurrentSection = parsed;
            if (parsed == SettingsSection.Logs)
            {
                _ = RefreshLogsAsync();
            }
        }
    }

    [RelayCommand]
    private void OpenInbounds() => CurrentSection = SettingsSection.Network;

    [RelayCommand]
    private void OpenAppProxySettings() => CurrentSection = SettingsSection.Routing;

    private void RefreshMuxQuicChoices()
    {
        var current = SelectedMuxXudpQuic?.Value
                      ?? (string.IsNullOrWhiteSpace(MuxXudpQuic) ? "reject" : MuxXudpQuic);
        MuxXudpQuicChoices.Clear();
        MuxXudpQuicChoices.Add(new NamedOption { Value = "reject", Display = L("MuxXudpQuicReject") });
        MuxXudpQuicChoices.Add(new NamedOption { Value = "allow", Display = L("MuxXudpQuicAllow") });
        SelectedMuxXudpQuic = MuxXudpQuicChoices.FirstOrDefault(o => o.Value == current)
                              ?? MuxXudpQuicChoices[0];
    }

    partial void OnSelectedMuxXudpQuicChanged(NamedOption? value)
    {
        if (value is not null)
        {
            MuxXudpQuic = value.Value;
        }
    }

    partial void OnFragmentEngineChanged(string value) => OnPropertyChanged(nameof(IsFragmentAdvanced));

    private void RefreshInboundsStatus()
    {
        InboundsStatusLabel = AllowLanConnections ? L("InboundsEnabled") : L("InboundsDisabled");
        OnPropertyChanged(nameof(InboundsStatusLabel));
    }

    partial void OnSelectedPreferredIpTypeChanged(string value)
    {
        switch (value)
        {
            case "IPv6":
                PreferIpv6 = true;
                Ipv6Enabled = true;
                break;
            case "Auto":
                PreferIpv6 = false;
                Ipv6Enabled = true;
                break;
            default:
                PreferIpv6 = false;
                Ipv6Enabled = false;
                break;
        }
    }

    [RelayCommand]
    private void OpenExcludedRoutes() => CurrentSection = SettingsSection.Routing;

    [RelayCommand]
    private void NavigateBack() => CurrentSection = SettingsSection.Root;

    private async Task LoadAsync()
    {
        _suppressPersist = true;
        try
        {
            var s = await _settings.GetAsync();
            AutoStartEnabled = s.AutoStart;
            KillSwitchEnabled = s.KillSwitch;
            DnsLeakProtection = s.DnsLeakProtection;
            AutoReconnect = s.AutoReconnect;
            AutoSelectFastest = s.AutoSelectFastestServer;
            AutoRefreshSubscriptions = s.AutoRefreshSubscriptions;
            UpdateSubscriptionsOnStart = s.UpdateSubscriptionsOnStart;
            ConnectOnStart = s.ConnectOnStart;
            PingOnStart = s.PingOnStart;
            SelectedPingType = s.PingType;
            AllowLanConnections = s.AllowLanConnections;
            BypassLocalForSystemProxy = s.BypassLocalForSystemProxy;
            UseSystemProxy = s.UseSystemProxy;
            UseLocalDns = s.UseLocalDns;
            UseDnsFromJson = s.UseDnsFromJson;
            EnableServerResolving = s.EnableServerResolving;
            IgnoreDuplicateServers = s.IgnoreDuplicateServers;
            PurgeOldProfilesOnSubscriptionUpdate = s.PurgeOldProfilesOnSubscriptionUpdate;
            SendHwidHeader = s.SendHwidHeader;
            CustomHwidValue = s.CustomHwidValue ?? string.Empty;
            MinimizeToTray = s.MinimizeToTray;
            UseTunMode = s.UseTunMode;
            SelectedTunProvider = s.TunProvider;
            TunMtu = s.TunMtu;
            SelectedTunStackMode = s.TunStackMode;
            TunDnsEnable = s.TunDnsEnable;
            SniffTraffic = s.SniffTraffic;
            RouteOnly = s.RouteOnly;
            Ipv6Enabled = s.Ipv6Enabled;
            PreferIpv6 = s.PreferIpv6;
            FakeDnsEnabled = s.FakeDnsEnabled;
            DomesticDns = s.DomesticDns;
            DnsHosts = s.DnsHosts;
            CoreLogLevel = string.IsNullOrWhiteSpace(s.CoreLogLevel) ? "warning" : s.CoreLogLevel;
            SocksEnableUdp = s.SocksEnableUdp;
            MuxEnabled = s.MuxEnabled;
            MuxConcurrency = s.MuxConcurrency > 0 ? s.MuxConcurrency : 8;
            MuxXudpConcurrency = s.MuxXudpConcurrency > 0 ? s.MuxXudpConcurrency : 8;
            MuxXudpQuic = string.IsNullOrWhiteSpace(s.MuxXudpQuic) ? "reject" : s.MuxXudpQuic;
            SelectedMuxXudpQuic = MuxXudpQuicChoices.FirstOrDefault(o => o.Value == MuxXudpQuic)
                                  ?? MuxXudpQuicChoices.FirstOrDefault();
            FragmentEnabled = s.FragmentEnabled;
            FragmentEngine = string.IsNullOrWhiteSpace(s.FragmentEngine) ? "Xray" : s.FragmentEngine;
            FragmentPackets = string.IsNullOrWhiteSpace(s.FragmentPackets) ? "tlshello" : s.FragmentPackets;
            FragmentLength = string.IsNullOrWhiteSpace(s.FragmentLength) ? "50-100" : s.FragmentLength;
            FragmentInterval = string.IsNullOrWhiteSpace(s.FragmentInterval) ? "10-20" : s.FragmentInterval;
            FragmentMaxSplit = string.IsNullOrWhiteSpace(s.FragmentMaxSplit) ? "10-20" : s.FragmentMaxSplit;
            NoiseEnabled = s.NoiseEnabled;
            NoiseRand = string.IsNullOrWhiteSpace(s.NoiseRand) ? "10-20" : s.NoiseRand;
            NoiseDelay = string.IsNullOrWhiteSpace(s.NoiseDelay) ? "10-16" : s.NoiseDelay;
            SelectedPreferredIpType = PreferIpv6 ? "IPv6" : Ipv6Enabled ? "Auto" : "IPv4";
            RefreshInboundsStatus();
            AutoSortAfterTest = s.AutoSortAfterTest;
            AutoRemoveInvalidAfterTest = s.AutoRemoveInvalidAfterTest;
            DelayTestUrl = string.IsNullOrWhiteSpace(s.DelayTestUrl) ? "https://www.gstatic.com/generate_204" : s.DelayTestUrl;
            IpApiUrl = string.IsNullOrWhiteSpace(s.IpApiUrl) ? "https://api.ip.sb/geoip" : s.IpApiUrl;
            RealPingConcurrency = s.RealPingConcurrency > 0 ? s.RealPingConcurrency : 16;
            SpeedDisplayEnabled = s.SpeedDisplayEnabled;
            ConfirmRemove = s.ConfirmRemove;
            CheckConflictsOnStart = s.CheckConflictsOnStart;
            RoutingEnabled = s.RoutingEnabled;
            SelectedUserAgent = s.SubscriptionUserAgent;
            SelectedRoutingMode = s.RoutingMode;
            DomainDirectRules = s.DomainDirectRules;
            SplitTunnelProcesses = string.Join(Environment.NewLine, s.SplitTunnelProcesses);
            SubscriptionRefreshIntervalMinutes = s.SubscriptionRefreshIntervalMinutes > 0
                ? s.SubscriptionRefreshIntervalMinutes
                : Math.Max(1, s.SubscriptionRefreshIntervalHours) * 60;
            if (!RefreshIntervalOptions.Contains(SubscriptionRefreshIntervalMinutes))
            {
                SubscriptionRefreshIntervalMinutes = 20;
            }
            DnsServer = s.DnsServer;
            RemoteDns = s.RemoteDns;
            LocalSocksPort = s.LocalSocksPort;
            LocalHttpPort = s.LocalHttpPort;
            SelectedLanguage = s.Language;
            SelectedTheme = s.Theme;
            Loc.SetLanguage(s.Language);
            SumraX.App.Themes.ThemeService.Apply(SelectedTheme);
            await ReloadSubscriptionUrlsAsync();
            ReloadSubscriptionTraffic(s);
            RefreshLanInfo();
        }
        finally
        {
            _suppressPersist = false;
        }
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_suppressPersist || string.IsNullOrEmpty(e.PropertyName))
        {
            return;
        }

        if (ShouldSkipAutoSave(e.PropertyName))
        {
            return;
        }

        ScheduleAutoSave();
    }

    private static bool ShouldSkipAutoSave(string propertyName)
    {
        if (propertyName.EndsWith("Label", StringComparison.Ordinal) ||
            propertyName.EndsWith("Title", StringComparison.Ordinal))
        {
            return true;
        }

        return propertyName is nameof(CurrentSection)
            or nameof(UpdateMessage)
            or nameof(NewSubscriptionUrl)
            or nameof(SelectedSubscriptionUrl)
            or nameof(LocalLanIp)
            or nameof(IsRootSection)
            or nameof(IsDetailSection)
            or nameof(SubscriptionUrls)
            or nameof(SubscriptionTrafficRows);
    }

    private void ScheduleAutoSave()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;
        _ = PersistAfterDelayAsync(token);
    }

    private async Task PersistAfterDelayAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(450, token);
            await SaveCoreAsync(showSavedMessage: false);
        }
        catch (OperationCanceledException)
        {
            // superseded by a newer change
        }
    }

    private void ReloadSubscriptionTraffic(AppSettings s)
    {
        SubscriptionTrafficRows.Clear();
        foreach (var entry in s.SubscriptionTraffic.Values.OrderBy(v => v.Url))
        {
            var used = entry.UploadBytes + entry.DownloadBytes;
            var traffic = entry.TotalBytes > 0
                ? $"{SubscriptionTrafficInfo.FormatBytes(used)} / {SubscriptionTrafficInfo.FormatBytes(entry.TotalBytes)}"
                : $"{SubscriptionTrafficInfo.FormatBytes(entry.UploadBytes)} ↑ {SubscriptionTrafficInfo.FormatBytes(entry.DownloadBytes)} ↓";
            var expire = entry.ExpireUtc == null
                ? L("SubTrafficNoExpiry")
                : entry.ExpireUtc <= DateTime.UtcNow
                    ? L("Expired")
                    : F("SubTrafficExpires", entry.ExpireUtc.Value.ToLocalTime().ToString("g"));

            SubscriptionTrafficRows.Add(new SubscriptionTrafficRowViewModel
            {
                Url = MaskSubscriptionUrl(entry.Url),
                TrafficText = traffic,
                ExpireText = expire
            });
        }
    }

    private static string MaskSubscriptionUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Query))
        {
            return uri.GetLeftPart(UriPartial.Path) + "?…";
        }

        return url.Length > 48 ? url[..24] + "…" + url[^16..] : url;
    }

    private async Task ReloadSubscriptionUrlsAsync()
    {
        SubscriptionUrls.Clear();
        foreach (var url in await _subRefresh.GetSubscriptionUrlsAsync())
        {
            SubscriptionUrls.Add(url);
        }
    }

    partial void OnAllowLanConnectionsChanged(bool value)
    {
        RefreshLanInfo();
        RefreshInboundsStatus();
        if (CurrentSection == SettingsSection.VpnHotspot)
        {
            RefreshVpnHotspotPanel();
        }
    }

    private void RefreshLanInfo()
    {
        LocalLanIp = AllowLanConnections ? WindowsVpnHotspotHelper.DetectShareHostIpv4() : "—";
        OnPropertyChanged(nameof(LocalLanIp));
    }

    private static string DetectLocalIp()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(addr.Address))
                    {
                        return addr.Address.ToString();
                    }
                }
            }
        }
        catch
        {
            // ignore
        }

        return "—";
    }

    partial void OnSelectedThemeChanged(AppTheme value)
    {
        SumraX.App.Themes.ThemeService.Apply(value);
    }

    [RelayCommand]
    private Task SaveAsync() => SaveCoreAsync(showSavedMessage: true);

    private async Task SaveCoreAsync(bool showSavedMessage)
    {
        try
        {
            var s = await _settings.GetAsync();
            s.AutoStart = AutoStartEnabled;
            s.KillSwitch = KillSwitchEnabled;
            s.DnsLeakProtection = DnsLeakProtection;
            s.AutoReconnect = AutoReconnect;
            s.AutoSelectFastestServer = AutoSelectFastest;
            s.AutoRefreshSubscriptions = AutoRefreshSubscriptions;
            s.UpdateSubscriptionsOnStart = UpdateSubscriptionsOnStart;
            s.ConnectOnStart = ConnectOnStart;
            s.PingOnStart = PingOnStart;
            s.PingType = SelectedPingType;
            s.AllowLanConnections = AllowLanConnections;
            s.BypassLocalForSystemProxy = BypassLocalForSystemProxy;
            s.UseSystemProxy = UseSystemProxy;
            s.UseLocalDns = UseLocalDns;
            s.UseDnsFromJson = UseDnsFromJson;
            s.EnableServerResolving = EnableServerResolving;
            s.IgnoreDuplicateServers = IgnoreDuplicateServers;
            s.PurgeOldProfilesOnSubscriptionUpdate = PurgeOldProfilesOnSubscriptionUpdate;
            s.SendHwidHeader = SendHwidHeader;
            s.CustomHwidValue = string.IsNullOrWhiteSpace(CustomHwidValue) ? null : CustomHwidValue.Trim();
            s.MinimizeToTray = MinimizeToTray;
            s.UseTunMode = UseTunMode;
            s.TunProvider = SelectedTunProvider;
            s.TunMtu = Math.Clamp(TunMtu, 576, 9000);
            s.TunStackMode = SelectedTunStackMode;
            s.TunDnsEnable = TunDnsEnable;
            s.SniffTraffic = SniffTraffic;
            s.RouteOnly = RouteOnly;
            s.Ipv6Enabled = Ipv6Enabled;
            s.PreferIpv6 = PreferIpv6;
            s.FakeDnsEnabled = FakeDnsEnabled;
            s.DomesticDns = DomesticDns.Trim();
            s.DnsHosts = DnsHosts.Trim();
            s.CoreLogLevel = string.IsNullOrWhiteSpace(CoreLogLevel) ? "warning" : CoreLogLevel.Trim();
            s.SocksEnableUdp = SocksEnableUdp;
            s.MuxEnabled = MuxEnabled;
            s.MuxConcurrency = Math.Clamp(MuxConcurrency, 1, 1024);
            s.MuxXudpConcurrency = Math.Clamp(MuxXudpConcurrency, 1, 1024);
            s.MuxXudpQuic = SelectedMuxXudpQuic?.Value
                           ?? (string.IsNullOrWhiteSpace(MuxXudpQuic) ? "reject" : MuxXudpQuic.Trim());
            s.FragmentEnabled = FragmentEnabled;
            s.FragmentEngine = string.IsNullOrWhiteSpace(FragmentEngine) ? "Xray" : FragmentEngine.Trim();
            s.FragmentPackets = string.IsNullOrWhiteSpace(FragmentPackets) ? "tlshello" : FragmentPackets.Trim();
            s.FragmentLength = string.IsNullOrWhiteSpace(FragmentLength) ? "50-100" : FragmentLength.Trim();
            s.FragmentInterval = string.IsNullOrWhiteSpace(FragmentInterval) ? "10-20" : FragmentInterval.Trim();
            s.FragmentMaxSplit = string.IsNullOrWhiteSpace(FragmentMaxSplit) ? "10-20" : FragmentMaxSplit.Trim();
            s.NoiseEnabled = NoiseEnabled;
            s.NoiseRand = string.IsNullOrWhiteSpace(NoiseRand) ? "10-20" : NoiseRand.Trim();
            s.NoiseDelay = string.IsNullOrWhiteSpace(NoiseDelay) ? "10-16" : NoiseDelay.Trim();
            s.AutoSortAfterTest = AutoSortAfterTest;
            s.AutoRemoveInvalidAfterTest = AutoRemoveInvalidAfterTest;
            s.DelayTestUrl = DelayTestUrl.Trim();
            s.IpApiUrl = IpApiUrl.Trim();
            s.RealPingConcurrency = Math.Clamp(RealPingConcurrency, 1, 64);
            s.SpeedDisplayEnabled = SpeedDisplayEnabled;
            s.ConfirmRemove = ConfirmRemove;
            s.CheckConflictsOnStart = CheckConflictsOnStart;
            s.RoutingEnabled = RoutingEnabled;
            s.SubscriptionUserAgent = SelectedUserAgent;
            s.RoutingMode = SelectedRoutingMode;
            s.DomainDirectRules = DomainDirectRules.Trim();
            s.SplitTunnelProcesses = SplitTunnelProcesses
                .Split(['\r', '\n', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            s.SubscriptionRefreshIntervalMinutes = SubscriptionRefreshIntervalMinutes;
            // Minutes are authoritative; keep the legacy hours field rounded up so it never under-shoots.
            s.SubscriptionRefreshIntervalHours = Math.Max(1, (SubscriptionRefreshIntervalMinutes + 59) / 60);
            s.DnsServer = DnsServer.Trim();
            s.RemoteDns = RemoteDns.Trim();
            s.LocalSocksPort = Math.Clamp(LocalSocksPort, 1024, 65535);
            s.LocalHttpPort = Math.Clamp(LocalHttpPort, 1024, 65535);
            if (s.LocalHttpPort == s.LocalSocksPort)
            {
                s.LocalHttpPort = s.LocalSocksPort >= 65535
                    ? s.LocalSocksPort - 1
                    : s.LocalSocksPort + 1;
            }
            s.Language = SelectedLanguage;
            s.Theme = SelectedTheme;
            await _settings.SaveAsync(s);
            await _autoStart.SetEnabledAsync(AutoStartEnabled);
            if (KillSwitchEnabled)
            {
                await _killSwitch.EnableAsync();
            }
            else
            {
                await _killSwitch.DisableAsync();
            }

            _suppressPersist = true;
            try
            {
                Loc.SetLanguage(SelectedLanguage);
                SumraX.App.Themes.ThemeService.Apply(SelectedTheme);
                LocalSocksPort = s.LocalSocksPort;
                LocalHttpPort = s.LocalHttpPort;
                if (showSavedMessage)
                {
                    UpdateMessage = L("SettingsSaved");
                }
            }
            finally
            {
                _suppressPersist = false;
            }
        }
        catch (Exception ex)
        {
            UpdateMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddSubscriptionUrlAsync()
    {
        var url = SubscriptionUrlNormalizer.Normalize(NewSubscriptionUrl);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        UpdateMessage = L("LoadingSubscription");
        await _subRefresh.RegisterSubscriptionUrlAsync(url);
        NewSubscriptionUrl = string.Empty;
        await ReloadSubscriptionUrlsAsync();

        var refresh = await _subRefresh.RefreshUrlAsync(url);
        if (!refresh.Success)
        {
            UpdateMessage = refresh.Errors.FirstOrDefault() ?? L("SubscriptionImportFailed");
            return;
        }

        UpdateMessage = F("SubRefreshCompleted", refresh.UpdatedServers, refresh.RemovedServers, refresh.Skipped);
        var settings = await _settings.GetAsync();
        ReloadSubscriptionTraffic(settings);
    }

    [RelayCommand]
    private async Task RemoveSubscriptionUrlAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedSubscriptionUrl))
        {
            return;
        }

        await _subRefresh.UnregisterSubscriptionUrlAsync(SelectedSubscriptionUrl);
        SelectedSubscriptionUrl = null;
        await ReloadSubscriptionUrlsAsync();
        UpdateMessage = L("SubscriptionUrlRemoved");
    }

    [RelayCommand]
    private async Task RefreshSubscriptionsAsync()
    {
        var urls = await _subRefresh.GetSubscriptionUrlsAsync();
        if (urls.Count == 0)
        {
            UpdateMessage = L("SubRefreshNone");
            return;
        }

        var result = await _subRefresh.RefreshAllAsync();
        if (!result.Success)
        {
            UpdateMessage = F("SubRefreshFailed", string.Join("; ", result.Errors));
            return;
        }

        UpdateMessage = F("SubRefreshCompleted", result.UpdatedServers, result.RemovedServers, result.Skipped);
        var settings = await _settings.GetAsync();
        ReloadSubscriptionTraffic(settings);
    }

    [RelayCommand]
    private async Task RunDiagnosticsAsync()
    {
        IsRunningDiagnostics = true;
        try
        {
            var report = await _diagnostics.RunAsync();
            DiagnosticRows.Clear();
            foreach (var item in report.Items)
            {
                DiagnosticRows.Add(new DiagnosticRowViewModel
                {
                    Name = item.Name,
                    Detail = item.Detail,
                    StatusGlyph = item.Status switch
                    {
                        DiagnosticStatus.Ok => "✓",
                        DiagnosticStatus.Warning => "!",
                        _ => "✗"
                    }
                });
            }

            UpdateMessage = report.AllOk ? L("DiagnosticsAllOk") : L("DiagnosticsIssuesFound");
        }
        finally
        {
            IsRunningDiagnostics = false;
        }
    }

    [RelayCommand]
    private async Task ExportDebugBundleAsync()
    {
        try
        {
            var path = await _debugExport.ExportBundleAsync();
            UpdateMessage = F("DebugBundleExported", path);
            if (TrustedBinaryGuard.TryValidateLocalPath(path, out var fullPath))
            {
                var explorer = TrustedBinaryGuard.GetWindowsBinary("explorer.exe");
                Process.Start(new ProcessStartInfo(explorer, $"/select,\"{fullPath}\"") { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            UpdateMessage = F("DebugBundleFailed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task RefreshLogsAsync()
    {
        LogText = await _logViewer.ReadTailAsync();
        OnPropertyChanged(nameof(LogText));
    }

    [RelayCommand]
    private async Task RunConflictCheckAsync()
    {
        var report = await _conflicts.DetectAsync();
        ConflictRows.Clear();
        foreach (var item in report.Items)
        {
            ConflictRows.Add(new DiagnosticRowViewModel
            {
                Name = item.Title,
                Detail = item.Detail,
                StatusGlyph = item.Severity switch
                {
                    ConflictSeverity.Info => "i",
                    ConflictSeverity.Warning => "!",
                    _ => "✗"
                }
            });
        }

        UpdateMessage = report.HasWarnings ? L("ConflictsFound") : L("ConflictsNone");
    }

    [RelayCommand]
    private async Task BackupAsync()
    {
        await _backup.BackupAsync();
        UpdateMessage = L("BackupCreated");
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        await _backup.RestoreLatestAsync();
        await LoadAsync();
        UpdateMessage = L("RestoredBackup");
    }
}
