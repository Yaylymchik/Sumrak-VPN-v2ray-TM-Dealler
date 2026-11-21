using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;

namespace SumraX.App.ViewModels;

public partial class SecurityViewModel : LocalizableViewModelBase, IDisposable
{
    private readonly IVpnEngine _vpn;
    private readonly ISettingsRepository _settings;
    private readonly IKillSwitchService _killSwitch;
    private bool _suppressPersist;

    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _pageSubtitle = "";
    [ObservableProperty] private string _statusTitle = "";
    [ObservableProperty] private string _statusDetail = "";
    [ObservableProperty] private string _modeLabel = "";
    [ObservableProperty] private string _serverLabel = "";
    [ObservableProperty] private bool _isProtected;
    [ObservableProperty] private bool _killSwitchEnabled;
    [ObservableProperty] private bool _dnsLeakProtection;
    [ObservableProperty] private bool _autoReconnect;
    [ObservableProperty] private string _killSwitchLabel = "";
    [ObservableProperty] private string _dnsLeakLabel = "";
    [ObservableProperty] private string _autoReconnectLabel = "";
    [ObservableProperty] private string _killSwitchDesc = "";
    [ObservableProperty] private string _dnsLeakDesc = "";
    [ObservableProperty] private string _autoReconnectDesc = "";
    [ObservableProperty] private string _openSpeedLabel = "";

    public event Action? OpenSpeedRequested;

    public SecurityViewModel(
        IVpnEngine vpn,
        ISettingsRepository settings,
        IKillSwitchService killSwitch,
        ILocalizationService loc) : base(loc)
    {
        _vpn = vpn;
        _settings = settings;
        _killSwitch = killSwitch;
        _vpn.StatusChanged += OnVpnStatusChanged;
        CompleteInitialization();
        _ = LoadAsync();
    }

    protected override void OnLanguageChanged()
    {
        PageTitle = L("SecurityPanelTitle");
        PageSubtitle = L("SecurityPanelSubtitle");
        KillSwitchLabel = L("KillSwitch");
        DnsLeakLabel = L("DnsLeakProtection");
        AutoReconnectLabel = L("AutoReconnect");
        KillSwitchDesc = L("KillSwitchDesc");
        DnsLeakDesc = L("DnsLeakDesc");
        AutoReconnectDesc = L("AutoReconnectDesc");
        OpenSpeedLabel = L("SpeedTest");
        RefreshStatus();
    }

    private void OnVpnStatusChanged(object? sender, ConnectionStatus status)
    {
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        IsProtected = _vpn.State == ConnectionState.Connected;
        StatusTitle = IsProtected ? L("SystemProtected") : L("SystemUnprotected");
        StatusDetail = IsProtected ? L("ProtectedStatus") : L("UnprotectedStatus");
        ModeLabel = _vpn.State == ConnectionState.Connected && _vpn.IsTunMode
            ? L("ModeBadgeTun")
            : L("ModeBadgeProxy");
        ServerLabel = string.IsNullOrWhiteSpace(_vpn.CurrentStatus.ServerName)
            ? "—"
            : _vpn.CurrentStatus.ServerName!;
    }

    private async Task LoadAsync()
    {
        var s = await _settings.GetAsync();
        _suppressPersist = true;
        try
        {
            KillSwitchEnabled = s.KillSwitch;
            DnsLeakProtection = s.DnsLeakProtection;
            AutoReconnect = s.AutoReconnect;
        }
        finally
        {
            _suppressPersist = false;
        }

        RefreshStatus();
    }

    partial void OnKillSwitchEnabledChanged(bool value) => _ = PersistAsync();
    partial void OnDnsLeakProtectionChanged(bool value) => _ = PersistAsync();
    partial void OnAutoReconnectChanged(bool value) => _ = PersistAsync();

    private async Task PersistAsync()
    {
        if (_suppressPersist)
        {
            return;
        }

        var s = await _settings.GetAsync();
        s.KillSwitch = KillSwitchEnabled;
        s.DnsLeakProtection = DnsLeakProtection;
        s.AutoReconnect = AutoReconnect;
        await _settings.SaveAsync(s);

        if (KillSwitchEnabled)
        {
            await _killSwitch.EnableAsync();
        }
        else
        {
            await _killSwitch.DisableAsync();
        }
    }

    [RelayCommand]
    private void OpenSpeedTest() => OpenSpeedRequested?.Invoke();

    public Task ReloadAsync() => LoadAsync();

    public void Dispose()
    {
        _vpn.StatusChanged -= OnVpnStatusChanged;
    }
}
