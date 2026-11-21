using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Threading;
using SumraX.App.Services;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Core.Vpn;
using SumraX.Infrastructure.Network;
using SumraX.Infrastructure.Security;
using SumraX.Infrastructure.Vpn;

namespace SumraX.App.ViewModels;

public partial class HomeViewModel : LocalizableViewModelBase
{
    private readonly IVpnEngine _vpn;
    private readonly IServerRepository _servers;
    private readonly ISettingsRepository _settings;
    private readonly ISpeedTestService _speedTest;
    private readonly IIpInfoService _ipInfo;
    private readonly LiveThroughputSampler _throughput = new();
    private ServerProfile? _activeServer;
    private CancellationTokenSource? _statsCts;

    [ObservableProperty] private ConnectionState _state = ConnectionState.Disconnected;
    [ObservableProperty] private string _connectButtonText = "";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _publicIp = "—";
    [ObservableProperty] private string _country = "—";
    [ObservableProperty] private string _pingDisplay = "—";
    [ObservableProperty] private string _downloadDisplay = "—";
    [ObservableProperty] private string _uploadDisplay = "—";
    [ObservableProperty] private string _durationDisplay = "00:00:00";
    [ObservableProperty] private string _errorMessage = "";
    [ObservableProperty] private string _serverName = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _ipLabel = "";
    [ObservableProperty] private string _locationLabel = "";
    [ObservableProperty] private string _pingLabel = "";
    [ObservableProperty] private string _durationLabel = "";
    [ObservableProperty] private string _downloadLabel = "";
    [ObservableProperty] private string _uploadLabel = "";
    [ObservableProperty] private string _speedTestLabel = "";
    [ObservableProperty] private bool _isPowerOn;
    [ObservableProperty] private bool _isConnecting;
    [ObservableProperty] private int _connectionProgress;
    [ObservableProperty] private string _countryFlag = "🌍";
    [ObservableProperty] private string _serverDisplayName = "";
    [ObservableProperty] private string _selectServerLabel = "";
    [ObservableProperty] private string _encryptingLabel = "";
    [ObservableProperty] private string _stayOnPageHint = "";
    [ObservableProperty] private string _serverLabel = "";
    [ObservableProperty] private string _encryptionLabel = "";
    [ObservableProperty] private string _trafficLabel = "";
    [ObservableProperty] private string _coreLabel = "";
    [ObservableProperty] private string _coreDisplay = "Xray";
    [ObservableProperty] private string _encryptionDisplay = "AES-256-GCM";
    [ObservableProperty] private string _trafficDisplay = "—";
    [ObservableProperty] private string _connectionStateLabel = "";
    [ObservableProperty] private bool _useTunMode = true;
    [ObservableProperty] private bool _isSmartMode = true;
    [ObservableProperty] private string _connectionModeProxyLabel = "";
    [ObservableProperty] private string _connectionModeTunLabel = "";
    [ObservableProperty] private string _connectionModeSmartLabel = "";
    [ObservableProperty] private string _connectionModeManualLabel = "";
    [ObservableProperty] private string _smartHint = "";
    [ObservableProperty] private string _terminalTypingLine = "";
    [ObservableProperty] private string _terminalTypingDisplay = "";
    [ObservableProperty] private bool _terminalCursorVisible = true;
    [ObservableProperty] private bool _showBlockServiceToast;
    [ObservableProperty] private string _blockServiceTitle = "BLOCK SERVICE";
    [ObservableProperty] private string _blockServiceSubtitle = "";
    [ObservableProperty] private string _subscriptionRenewalBanner = "";
    [ObservableProperty] private bool _hasServers;
    [ObservableProperty] private string _homeEmptyHint = "";
    [ObservableProperty] private string _homeAiLabel = "";
    [ObservableProperty] private string _homeBrandCoder = "YayLym";
    [ObservableProperty] private string _homeBrandName = "Sumrak";
    [ObservableProperty] private string _compactSpeedDisplay = "";
    [ObservableProperty] private bool _serversListHidden;
    [ObservableProperty] private string _hideAllLabel = "";
    [ObservableProperty] private string _hwidDisplay = "";

    /// <summary>Hide idle status line when server groups are visible.</summary>
    public bool ShowHomeStatusText =>
        !HasServers || State is ConnectionState.Connected or ConnectionState.Connecting
            or ConnectionState.Reconnecting or ConnectionState.Error;

    public System.Collections.ObjectModel.ObservableCollection<string> TerminalLines { get; } = [];

    private const int MaxTerminalLines = 16;
    private readonly DispatcherTimer _terminalFeedTimer;
    private readonly DispatcherTimer _terminalTypeTimer;
    private readonly DispatcherTimer _terminalCursorTimer;
    private string _pendingLine = "";
    private int _typedChars;
    private ConnectionState _terminalState = ConnectionState.Disconnected;
    private DispatcherTimer? _progressTimer;
    private CancellationTokenSource? _connectCts;
    private CancellationTokenSource? _blockServiceToastCts;
    private bool _wasConnected;

    public HomeViewModel(
        IVpnEngine vpn,
        IServerRepository servers,
        ISettingsRepository settings,
        ISpeedTestService speedTest,
        IIpInfoService ipInfo,
        ILocalizationService loc) : base(loc)
    {
        _vpn = vpn;
        _servers = servers;
        _settings = settings;
        _speedTest = speedTest;
        _ipInfo = ipInfo;
        _vpn.StatusChanged += OnStatusChanged;

        TerminalLines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(TerminalTypingDisplay));

        _terminalFeedTimer = new DispatcherTimer();
        _terminalFeedTimer.Tick += (_, _) => QueueTerminalLine();
        ScheduleNextTerminalFeed();

        _terminalTypeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(28) };
        _terminalTypeTimer.Tick += OnTerminalTypeTick;

        _terminalCursorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520) };
        _terminalCursorTimer.Tick += (_, _) =>
        {
            TerminalCursorVisible = !TerminalCursorVisible;
            UpdateTerminalTypingDisplay();
        };
        _terminalCursorTimer.Start();

        TerminalLines.Add("> sumrax kernel 6.4.0 — secure mode");
        TerminalLines.Add("> initializing cyber terminal...");
        _terminalState = _vpn.State;
        QueueTerminalLine(force: true);

        CompleteInitialization();
        _ = RefreshHwidAsync();
        _ = LoadSelectedServerAsync();
        _ = LoadConnectionModeAsync();
        _ = RefreshSubscriptionRenewalBannerAsync();
    }

    private async Task RefreshSubscriptionRenewalBannerAsync()
    {
        var settings = await _settings.GetAsync();
        SubscriptionRenewalBanner = "";
        foreach (var pair in settings.SubscriptionTraffic)
        {
            var days = SubscriptionRenewalHelper.RemainingDays(pair.Value);
            var banner = SubscriptionRenewalHelper.HomeBannerRu(days, settings.Language == AppLanguage.Russian);
            if (!string.IsNullOrEmpty(banner))
            {
                SubscriptionRenewalBanner = banner;
                if (days is >= 0 and <= 7)
                {
                    BlockServiceTitle = banner;
                    BlockServiceSubtitle = days == 0
                        ? (settings.Language == AppLanguage.Russian ? "Подписка истекла" : "Subscription expired")
                        : "";
                    ShowBlockServiceToast = true;
                }

                break;
            }
        }
    }

    private bool _suppressModePersist = true;

    private async Task LoadConnectionModeAsync()
    {
        var settings = await _settings.GetAsync();
        UseTunMode = settings.UseTunMode;
        IsSmartMode = settings.SmartConnectionMode;
        _suppressModePersist = false;
    }

    partial void OnUseTunModeChanged(bool value)
    {
        if (_suppressModePersist)
        {
            return;
        }

        _ = PersistConnectionModeAsync(tunMode: value, smartMode: null);
    }

    partial void OnIsSmartModeChanged(bool value)
    {
        if (_suppressModePersist)
        {
            return;
        }

        _ = PersistConnectionModeAsync(tunMode: null, smartMode: value);
    }

    private async Task PersistConnectionModeAsync(bool? tunMode, bool? smartMode)
    {
        try
        {
            var s = await _settings.GetAsync();
            var changed = false;
            if (tunMode is bool tun && s.UseTunMode != tun)
            {
                s.UseTunMode = tun;
                changed = true;
            }

            if (smartMode is bool smart && s.SmartConnectionMode != smart)
            {
                s.SmartConnectionMode = smart;
                changed = true;
            }

            if (changed)
            {
                await _settings.SaveAsync(s);
            }
        }
        catch
        {
            // ignore persistence errors on quick toggle
        }
    }

    private async Task LoadSelectedServerAsync()
    {
        var settings = await _settings.GetAsync();
        var all = (await _servers.GetAllAsync()).Where(s => CoreSelector.IsSupported(s.Protocol)).ToList();
        HasServers = all.Count > 0;
        _activeServer = !string.IsNullOrWhiteSpace(settings.LastSelectedServerId)
            ? all.FirstOrDefault(s => s.Id == settings.LastSelectedServerId) ?? all.FirstOrDefault()
            : all.FirstOrDefault();
        UpdateServerDisplay();
    }

    public void RefreshSelectedServer() => _ = LoadSelectedServerAsync();

    public async Task RunStartupRoutineAsync()
    {
        var settings = await _settings.GetAsync();
        var all = (await _servers.GetAllAsync()).Where(s => CoreSelector.IsSupported(s.Protocol)).ToList();
        if (all.Count == 0)
        {
            return;
        }

        _activeServer = !string.IsNullOrWhiteSpace(settings.LastSelectedServerId)
            ? all.FirstOrDefault(s => s.Id == settings.LastSelectedServerId) ?? all[0]
            : all[0];

        if (settings.PingOnStart)
        {
            await _speedTest.MeasureAllPingsAsync(all);
            if (settings.AutoSelectFastestServer)
            {
                _activeServer = all.Where(s => s.PingMs > 0).MinBy(s => s.PingMs) ?? _activeServer;
            }
        }

        UpdateServerDisplay();

        if (!settings.ConnectOnStart || State != ConnectionState.Disconnected)
        {
            return;
        }

        if (settings.AutoSelectFastestServer && !settings.PingOnStart)
        {
            await _speedTest.MeasureAllPingsAsync(all);
            _activeServer = all.Where(s => s.PingMs > 0).MinBy(s => s.PingMs) ?? _activeServer;
        }

        IsBusy = true;
        try
        {
            await _vpn.ConnectAsync(_activeServer, BeginConnectAttempt());
        }
        finally
        {
            EndConnectAttempt();
            IsBusy = false;
        }
    }

    private CancellationToken BeginConnectAttempt()
    {
        _connectCts?.Cancel();
        _connectCts?.Dispose();
        _connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        return _connectCts.Token;
    }

    private void EndConnectAttempt()
    {
        _connectCts?.Dispose();
        _connectCts = null;
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (State is ConnectionState.Connecting or ConnectionState.Reconnecting)
        {
            _connectCts?.Cancel();
            _statsCts?.Cancel();
            await _vpn.DisconnectAsync();
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (State == ConnectionState.Connected)
            {
                _statsCts?.Cancel();
                await _vpn.DisconnectAsync();
                return;
            }

            var settings = await _settings.GetAsync();
            var all = (await _servers.GetAllAsync()).Where(s => CoreSelector.IsSupported(s.Protocol)).ToList();
            if (all.Count == 0)
            {
                ErrorMessage = L("NoServers");
                StatusText = L("NotConnected");
                return;
            }

            ErrorMessage = "";

            _activeServer = !string.IsNullOrWhiteSpace(settings.LastSelectedServerId)
                ? all.FirstOrDefault(s => s.Id == settings.LastSelectedServerId) ?? all[0]
                : all[0];

            UpdateServerDisplay();

            // Smart mode (v2rayNG): real-ping all, pick best reachable, then connect.
            if (settings.SmartConnectionMode)
            {
                StatusText = L("SmartScanning");
                ConnectionStateLabel = "SMART SCAN";
                await _speedTest.MeasureAllPingsAsync(all, BeginConnectAttempt());
                // Rank by delay + load + history instead of raw ping, so a saturated near node
                // does not beat a slightly slower idle one.
                var best = SmartProfileRanker.PickBest(all, preferFavorite: true);
                if (best == null)
                {
                    ErrorMessage = L("SmartNoServers");
                    StatusText = L("SystemUnprotected");
                    ConnectionStateLabel = "DISCONNECTED";
                    return;
                }

                _activeServer = best;
                settings.LastSelectedServerId = best.Id;
                await _settings.SaveAsync(settings);
                foreach (var s in all.Where(x => x.PingMs > 0))
                {
                    await _servers.UpdateMetricsAsync(s.Id, s.PingMs, s.LoadPercent, s.DownloadMbps, s.UploadMbps);
                }

                UpdateServerDisplay();
                StatusText = F("SmartFound", best.Name, best.PingMs);
            }
            else if (settings.AutoSelectFastestServer)
            {
                await _speedTest.MeasureAllPingsAsync(all, BeginConnectAttempt());
                _activeServer = SmartProfileRanker.PickBest(all, preferFavorite: true) ?? _activeServer;
                UpdateServerDisplay();
            }

            await _vpn.ConnectAsync(_activeServer, BeginConnectAttempt());
        }
        finally
        {
            EndConnectAttempt();
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RunSpeedTestAsync()
    {
        if (State != ConnectionState.Connected)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var (down, up) = await _speedTest.MeasureSpeedAsync();
            DownloadDisplay = $"{down:F1} Mbps";
            UploadDisplay = $"{up:F1} Mbps";
            TrafficDisplay = $"↓{down:F0} ↑{up:F0} Mbps";
            if (_activeServer != null)
            {
                await _servers.UpdateMetricsAsync(_activeServer.Id, _activeServer.PingMs, _activeServer.LoadPercent, down, up);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnStatusChanged(object? sender, ConnectionStatus status)
    {
        Application.Current?.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            try
            {
                State = status.State;
                IsPowerOn = status.State == ConnectionState.Connected;
                IsConnecting = status.State is ConnectionState.Connecting or ConnectionState.Reconnecting;
                UpdateConnectionProgress(status.State);
                DurationDisplay = FormatDuration(status.ConnectedDuration);
                ServerName = status.ServerName ?? "";
                ErrorMessage = status.State == ConnectionState.Error
                    ? status.ErrorMessage ?? L("ConnectionFailed")
                    : "";
                if (!string.IsNullOrWhiteSpace(status.PublicIp))
                {
                    PublicIp = status.PublicIp;
                }
                if (!string.IsNullOrWhiteSpace(status.Country))
                {
                    Country = status.Country;
                }
                RefreshLabels();
                SetTerminalState(status.State);
                if (status.State == ConnectionState.Error && !string.IsNullOrWhiteSpace(status.ErrorMessage))
                {
                    TerminalLines.Add("> " + TrimTerminalError(status.ErrorMessage));
                    TrimTerminalLines();
                }
                if (status.State == ConnectionState.Connected)
                {
                    ShowBlockServiceBanner(animateOnlyOnTransition: !_wasConnected);
                    if (!_wasConnected)
                    {
                        _ = StartStatsLoopAsync();
                    }
                }
                else
                {
                    ShowBlockServiceToast = false;
                    _statsCts?.Cancel();
                    if (status.State == ConnectionState.Disconnected)
                    {
                        ResetLiveStats();
                    }
                }

                _wasConnected = status.State == ConnectionState.Connected;
            }
            catch (Exception)
            {
                // UI animation/bindings must not tear down an active VPN session.
            }
        });
    }

    private async Task StartStatsLoopAsync()
    {
        _statsCts?.Cancel();
        _statsCts = new CancellationTokenSource();
        var token = _statsCts.Token;
        _throughput.Reset();

        try
        {
            var (ip, country, _) = await _ipInfo.GetPublicIpAsync(!_vpn.IsTunMode, token);
            if (!token.IsCancellationRequested)
            {
                PublicIp = string.IsNullOrWhiteSpace(ip) ? "—" : ip;
                Country = string.IsNullOrWhiteSpace(country) ? "—" : country;
            }

            if (_activeServer != null)
            {
                var ping = await _speedTest.MeasurePingAsync(_activeServer, token);
                if (!token.IsCancellationRequested)
                {
                    PingDisplay = ping > 0 ? F("PingMsFormat", ping) : "—";
                }
            }

            while (!token.IsCancellationRequested && _vpn.State == ConnectionState.Connected)
            {
                var status = _vpn.CurrentStatus;
                DurationDisplay = FormatDuration(status.ConnectedDuration);

                var (down, up) = _throughput.Sample();
                DownloadDisplay = FormatMbps(down);
                UploadDisplay = FormatMbps(up);
                TrafficDisplay = down < 0.05 && up < 0.05
                    ? "↓— ↑—"
                    : $"↓{FormatMbpsShort(down)} ↑{FormatMbpsShort(up)}";

                CoreDisplay = _vpn.IsTunMode ? "Xray · TUN" : "Xray · Proxy";
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException)
        {
            // expected
        }
    }

    private void ResetLiveStats()
    {
        DurationDisplay = "00:00:00";
        DownloadDisplay = "—";
        UploadDisplay = "—";
        TrafficDisplay = "—";
        PublicIp = "—";
        Country = "—";
        PingDisplay = "—";
        CoreDisplay = UseTunMode ? "Xray · TUN" : "Xray · Proxy";
        _throughput.Reset();
    }

    private static string FormatMbps(double mbps) =>
        mbps < 0.05 ? "—" : $"{mbps:0.0} Mbps";

    private static string FormatMbpsShort(double mbps) =>
        mbps < 0.05 ? "—" : $"{mbps:0.0}";

    private void ShowBlockServiceBanner(bool animateOnlyOnTransition)
    {
        _blockServiceToastCts?.Cancel();
        _blockServiceToastCts?.Dispose();
        _blockServiceToastCts = null;
        BlockServiceTitle = L("BlockServiceTitle");
        BlockServiceSubtitle = L("BlockServiceSubtitle");
        // Keep visible for the whole connected session
        if (!ShowBlockServiceToast || animateOnlyOnTransition)
        {
            ShowBlockServiceToast = true;
        }
    }

    private static string FormatDuration(TimeSpan ts) =>
        $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

    protected override void OnLanguageChanged()
    {
        RefreshLabels();
    }

    private void RefreshLabels()
    {
        IpLabel = L("HudIp");
        LocationLabel = L("HudLocation");
        PingLabel = L("HudPing");
        DurationLabel = L("HudDuration");
        DownloadLabel = L("Download");
        UploadLabel = L("Upload");
        SpeedTestLabel = L("SpeedTest");
        SelectServerLabel = L("SelectServer");
        EncryptingLabel = L("Encrypting");
        StayOnPageHint = L("StayOnPageHint");
        ServerLabel = L("HudServer");
        EncryptionLabel = L("HudEncryption");
        TrafficLabel = L("HudTraffic");
        CoreLabel = L("HudCore");
        if (State != ConnectionState.Connected)
        {
            CoreDisplay = UseTunMode ? "Xray · TUN" : "Xray · Proxy";
        }
        ConnectionModeProxyLabel = L("ConnectionModeProxy");
        ConnectionModeTunLabel = L("ConnectionModeTun");
        ConnectionModeSmartLabel = L("ConnectionModeSmart");
        ConnectionModeManualLabel = L("ConnectionModeManual");
        HomeEmptyHint = L("HomeEmptyHint");
        HomeAiLabel = L("HomeAiHelper");
        HideAllLabel = ServersListHidden ? L("ShowEverything") : L("HideEverything");
        SmartHint = L("SmartTapHint");
        BlockServiceTitle = L("BlockServiceTitle");
        BlockServiceSubtitle = L("BlockServiceSubtitle");
        ConnectButtonText = State switch
        {
            ConnectionState.Connected => L("Disconnect"),
            ConnectionState.Connecting or ConnectionState.Reconnecting => L("Connecting"),
            _ => L("CreateSecureConnection")
        };
        StatusText = State switch
        {
            ConnectionState.Connected => L("SystemProtected"),
            ConnectionState.Connecting or ConnectionState.Reconnecting => L("Connecting"),
            ConnectionState.Error => L("ConnectionFailed"),
            _ => L("SystemUnprotected")
        };
        ConnectionStateLabel = State switch
        {
            ConnectionState.Connected => "CONNECTED",
            ConnectionState.Connecting or ConnectionState.Reconnecting => "CONNECTING",
            ConnectionState.Error => "ERROR",
            _ => "DISCONNECTED"
        };
        EncryptionDisplay = _activeServer != null
            ? $"{_activeServer.Protocol} / AES-256"
            : "AES-256-GCM";
        UpdateServerDisplay();
    }

    private static string TrimTerminalError(string message)
    {
        var line = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return line.Length <= 120 ? line : line[..117] + "...";
    }

    private void SetTerminalState(ConnectionState state)
    {
        if (_terminalState == state)
        {
            return;
        }

        FlushTerminalTyping();
        _terminalState = state;
        TerminalLines.Add($"> {CyberTerminalFeed.SessionBanner(state)}");
        TrimTerminalLines();
        QueueTerminalLine(force: true);
    }

    private void ScheduleNextTerminalFeed()
    {
        _terminalFeedTimer.Stop();
        _terminalFeedTimer.Interval = TimeSpan.FromMilliseconds(Random.Shared.Next(900, 2400));
        _terminalFeedTimer.Start();
    }

    private void QueueTerminalLine(bool force = false)
    {
        ScheduleNextTerminalFeed();

        if (!force && (!string.IsNullOrEmpty(_pendingLine) || _terminalTypeTimer.IsEnabled))
        {
            return;
        }

        var ping = ParsePingMs(PingDisplay);
        var protocol = _activeServer?.Protocol.ToString();
        _pendingLine = CyberTerminalFeed.Next(_terminalState, _activeServer?.Name, protocol, ping);
        _typedChars = 0;
        TerminalTypingLine = "";
        UpdateTerminalTypingDisplay();
        _terminalTypeTimer.Start();
    }

    private void OnTerminalTypeTick(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_pendingLine))
        {
            _terminalTypeTimer.Stop();
            UpdateTerminalTypingDisplay();
            return;
        }

        _typedChars++;
        TerminalTypingLine = _pendingLine[..Math.Min(_typedChars, _pendingLine.Length)];
        UpdateTerminalTypingDisplay();

        if (_typedChars < _pendingLine.Length)
        {
            return;
        }

        TerminalLines.Add("> " + _pendingLine);
        _pendingLine = "";
        TerminalTypingLine = "";
        TrimTerminalLines();
        _terminalTypeTimer.Stop();
        UpdateTerminalTypingDisplay();
    }

    private void FlushTerminalTyping()
    {
        if (string.IsNullOrEmpty(_pendingLine))
        {
            return;
        }

        var text = _typedChars >= _pendingLine.Length
            ? _pendingLine
            : _pendingLine[.._typedChars];
        if (!string.IsNullOrEmpty(text))
        {
            TerminalLines.Add("> " + text + "…");
            TrimTerminalLines();
        }

        _pendingLine = "";
        _typedChars = 0;
        TerminalTypingLine = "";
        _terminalTypeTimer.Stop();
        UpdateTerminalTypingDisplay();
    }

    private void TrimTerminalLines()
    {
        while (TerminalLines.Count > MaxTerminalLines)
        {
            TerminalLines.RemoveAt(0);
        }
    }

    private void UpdateTerminalTypingDisplay()
    {
        TerminalTypingDisplay = TerminalTypingLine + (TerminalCursorVisible ? "▌" : " ");
    }

    partial void OnTerminalTypingLineChanged(string value) => UpdateTerminalTypingDisplay();

    private static int ParsePingMs(string display)
    {
        if (string.IsNullOrWhiteSpace(display) || display == "—")
        {
            return 0;
        }

        var digits = new string(display.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var ms) ? ms : 0;
    }

    private void UpdateServerDisplay()
    {
        if (_activeServer == null)
        {
            ServerDisplayName = L("SelectServer");
            CountryFlag = "🌍";
            return;
        }

        ServerDisplayName = _activeServer.DisplayName;
        CountryFlag = ToFlagEmoji(_activeServer.CountryCode);
        if (string.IsNullOrWhiteSpace(Country) || Country == "—")
        {
            Country = _activeServer.CountryName;
        }
    }

    private void UpdateConnectionProgress(ConnectionState state)
    {
        if (state is ConnectionState.Connecting or ConnectionState.Reconnecting)
        {
            if (_progressTimer == null)
            {
                _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
                _progressTimer.Tick += (_, _) =>
                {
                    ConnectionProgress = ConnectionProgress >= 4 ? 1 : ConnectionProgress + 1;
                };
            }

            if (!_progressTimer.IsEnabled)
            {
                ConnectionProgress = 1;
                _progressTimer.Start();
            }
        }
        else
        {
            _progressTimer?.Stop();
            ConnectionProgress = state == ConnectionState.Connected ? 4 : 0;
        }
    }

    partial void OnServersListHiddenChanged(bool value)
    {
        HideAllLabel = value ? L("ShowEverything") : L("HideEverything");
    }

    partial void OnHasServersChanged(bool value) => OnPropertyChanged(nameof(ShowHomeStatusText));

    partial void OnStateChanged(ConnectionState value)
    {
        OnPropertyChanged(nameof(ShowHomeStatusText));
    }

    private async Task RefreshHwidAsync()
    {
        var hwid = await HwidManager.ResolveHwidAsync(_settings);
        HwidDisplay = string.IsNullOrWhiteSpace(hwid) ? L("HwidUnavailable") : F("HwidFormat", hwid);
    }

    [RelayCommand]
    private async Task CopyHwidAsync()
    {
        var hwid = await HwidManager.ResolveHwidAsync(_settings);
        if (!string.IsNullOrWhiteSpace(hwid))
        {
            Clipboard.SetText(hwid);
        }
    }

    [RelayCommand]
    private void ToggleServersListHidden()
    {
        if (!HasServers)
        {
            return;
        }

        ServersListHidden = !ServersListHidden;
    }

    private static string ToFlagEmoji(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 2 || code.Equals("UN", StringComparison.OrdinalIgnoreCase))
        {
            return "🌍";
        }

        code = code.ToUpperInvariant();
        return string.Concat(code.Select(c => char.ConvertFromUtf32(0x1F1E6 + (c - 'A'))));
    }
}
