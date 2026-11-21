using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SumraX.App.Markup;
using SumraX.Core.Interfaces;
using SumraX.Infrastructure.Vpn;

namespace SumraX.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILocalizationService _loc;
    private readonly ISettingsRepository _settings;
    private readonly IVpnEngine _vpn;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _appTitle = "Sumrak VPN";
    [ObservableProperty] private string _versionText = "v1.0.0";
    [ObservableProperty] private string _navHome = "Home";
    [ObservableProperty] private string _navSecurity = "Security";
    [ObservableProperty] private string _navSpeed = "Speed";
    [ObservableProperty] private string _navSettings = "Settings";
    [ObservableProperty] private string _navAi = "SumraX AI";
    [ObservableProperty] private string _modeBadgeLabel = "VPN // SECURE CORE";
    [ObservableProperty] private string _coreLabel = "Xray-core";
    [ObservableProperty] private string _sectionCore = "[ CORE ]";
    [ObservableProperty] private string _sectionSecurity = "[ SECURITY ]";
    [ObservableProperty] private string _sectionAi = "[ AI ]";
    [ObservableProperty] private string _sectionSystem = "[ SYSTEM ]";
    [ObservableProperty] private string _sidebarToggle = "Sidebar";
    [ObservableProperty] private string _sidebarCollapse = "Collapse";
    [ObservableProperty] private string _sidebarExpand = "Pin open";
    [ObservableProperty] private int _selectedNavIndex;
    [ObservableProperty] private bool _isServerPickerOpen;
    [ObservableProperty] private bool _isSidebarExpanded;
    [ObservableProperty] private bool _isSidebarPinned;
    [ObservableProperty] private bool _suppressHoverExpand;
    [ObservableProperty] private string _homeAddSubscriptionLabel = "";

    public bool ShowSidebarRail => SelectedNavIndex != 0;

    public HomeViewModel Home { get; }
    public ServersViewModel Servers { get; }
    public SpeedViewModel Speed { get; }
    public SecurityViewModel Security { get; }
    public SettingsViewModel Settings { get; }
    public AiViewModel Ai { get; }

    public MainViewModel(
        ILocalizationService loc,
        ISettingsRepository settings,
        IVpnEngine vpn,
        HomeViewModel home,
        ServersViewModel servers,
        SpeedViewModel speed,
        SecurityViewModel security,
        SettingsViewModel settingsVm,
        AiViewModel ai)
    {
        _loc = loc;
        _settings = settings;
        _vpn = vpn;
        Home = home;
        Servers = servers;
        Speed = speed;
        Security = security;
        Settings = settingsVm;
        Ai = ai;
        _currentPage = home;
        _versionText = $"v{AppInfo.Version}";
        _loc.LanguageChanged += (_, _) => RefreshTitles();
        _vpn.StatusChanged += (_, _) => RefreshModeBadge();
        Servers.ServerPicked += () => CloseServerPickerCommand.Execute(null);
        Security.OpenSpeedRequested += () => SelectedNavIndex = 1;
        RefreshTitles();
    }

    partial void OnSelectedNavIndexChanged(int value)
    {
        OnPropertyChanged(nameof(ShowSidebarRail));
        CurrentPage = value switch
        {
            0 => Home,
            1 => Speed,
            2 => Security,
            3 => Ai,
            4 => Settings,
            _ => Home
        };

        if (value == 2)
        {
            _ = Security.ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task OpenServersAsync()
    {
        await Servers.RefreshListAsync();
        IsServerPickerOpen = true;
    }

    [RelayCommand]
    private void OpenHomeAddSubscription() => OpenSettingsSubscriptions();

    [RelayCommand]
    private void OpenSettings() => SelectedNavIndex = 4;

    [RelayCommand]
    private void OpenAi() => SelectedNavIndex = 3;

    [RelayCommand]
    private void CloseServerPicker() => IsServerPickerOpen = false;

    [RelayCommand]
    private void ToggleSidebar()
    {
        if (IsSidebarExpanded)
        {
            // Explicit collapse — keep closed until the cursor leaves the rail
            IsSidebarPinned = false;
            IsSidebarExpanded = false;
            SuppressHoverExpand = true;
        }
        else
        {
            IsSidebarPinned = true;
            IsSidebarExpanded = true;
            SuppressHoverExpand = false;
        }
    }

    public void ExpandSidebarTransient()
    {
        if (SuppressHoverExpand || IsSidebarPinned)
        {
            return;
        }

        IsSidebarExpanded = true;
    }

    public void CollapseSidebarIfUnpinned()
    {
        SuppressHoverExpand = false;
        if (!IsSidebarPinned)
        {
            IsSidebarExpanded = false;
        }
    }

    public void OpenSettingsRouting()
    {
        SelectedNavIndex = 4;
        Settings.CurrentSection = SettingsSection.Routing;
    }

    public void OpenSettingsSubscriptions(string? subscriptionUrl = null)
    {
        SelectedNavIndex = 4;
        Settings.NavigateToSubscription(subscriptionUrl);
    }

    private void RefreshTitles()
    {
        AppTitle = _loc.Get("AppTitle");
        NavHome = _loc.Get("NavHome");
        NavSecurity = _loc.Get("NavSecurity");
        NavSpeed = _loc.Get("NavSpeed");
        NavSettings = _loc.Get("NavSettings");
        NavAi = _loc.Get("NavAiCore");
        SectionCore = _loc.Get("SectionCore");
        SectionSecurity = _loc.Get("SectionSecurity");
        SectionAi = _loc.Get("SectionAi");
        SectionSystem = _loc.Get("SectionSystem");
        SidebarToggle = _loc.Get("SidebarToggle");
        SidebarCollapse = _loc.Get("SidebarCollapse");
        SidebarExpand = _loc.Get("SidebarExpand");
        HomeAddSubscriptionLabel = _loc.Get("HomeAddSubscription");
        _ = RefreshModeBadgeAsync();
    }

    private async Task RefreshModeBadgeAsync()
    {
        var s = await _settings.GetAsync();
        if (_vpn.State == SumraX.Core.Enums.ConnectionState.Connected && _vpn.IsTunMode)
        {
            ModeBadgeLabel = _loc.Get("ModeBadgeTun");
            CoreLabel = s.TunProvider == SumraX.Core.Enums.TunProvider.Tun2Proxy
                ? _loc.Get("Tun2ProxyCore")
                : _loc.Get("SingBoxCore");
            OnPropertyChanged(nameof(ModeBadgeLabel));
            OnPropertyChanged(nameof(CoreLabel));
            return;
        }

        if (s.UseTunMode && TunSupport.IsAvailable())
        {
            ModeBadgeLabel = _loc.Get("ModeBadgeTun");
            CoreLabel = s.TunProvider == SumraX.Core.Enums.TunProvider.Tun2Proxy
                ? _loc.Get("Tun2ProxyCore")
                : _loc.Get("SingBoxCore");
        }
        else
        {
            ModeBadgeLabel = _loc.Get("ModeBadgeProxy");
            CoreLabel = _loc.Get("XrayCore");
        }

        OnPropertyChanged(nameof(ModeBadgeLabel));
        OnPropertyChanged(nameof(CoreLabel));
    }

    private void RefreshModeBadge() => _ = RefreshModeBadgeAsync();
}
