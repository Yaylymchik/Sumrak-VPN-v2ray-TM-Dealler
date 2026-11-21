using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;

namespace SumraX.App.ViewModels;

public partial class SpeedViewModel : LocalizableViewModelBase, IDisposable
{
    private readonly IVpnEngine _vpn;
    private readonly ISpeedTestService _speedTest;
    private readonly IIpInfoService _ipInfo;
    private CancellationTokenSource? _testCts;

    [ObservableProperty] private string _downloadDisplay = "—";
    [ObservableProperty] private string _uploadDisplay = "—";
    [ObservableProperty] private string _pingDisplay = "—";
    [ObservableProperty] private string _idlePingDisplay = "—";
    [ObservableProperty] private string _downloadPingDisplay = "—";
    [ObservableProperty] private string _uploadPingDisplay = "—";
    [ObservableProperty] private string _statusHint = "";
    [ObservableProperty] private string _goLabel = "GO";
    [ObservableProperty] private string _serverInfo = "—";
    [ObservableProperty] private string _clientInfo = "—";
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private bool _showGauge;
    [ObservableProperty] private bool _showGoButton = true;
    [ObservableProperty] private double _gaugeSpeed;
    [ObservableProperty] private string _gaugeUnit = "Mbps";
    [ObservableProperty] private SpeedTestPhase _phase = SpeedTestPhase.Idle;
    [ObservableProperty] private string _downloadTabLabel = "";
    [ObservableProperty] private string _uploadTabLabel = "";
    [ObservableProperty] private string _idlePingLabel = "";
    [ObservableProperty] private string _downloadPingLabel = "";
    [ObservableProperty] private string _uploadPingLabel = "";

    public SpeedViewModel(
        IVpnEngine vpn,
        ISpeedTestService speedTest,
        IIpInfoService ipInfo,
        ILocalizationService loc) : base(loc)
    {
        _vpn = vpn;
        _speedTest = speedTest;
        _ipInfo = ipInfo;
        _vpn.StatusChanged += OnVpnStatusChanged;
        IsConnected = _vpn.State == ConnectionState.Connected;
        CompleteInitialization();
        ApplyConnectionState();
    }

    protected override void OnLanguageChanged() => RefreshLabels();

    private void RefreshLabels()
    {
        DownloadTabLabel = L("DownloadMbps");
        UploadTabLabel = L("UploadMbps");
        IdlePingLabel = L("IdlePing");
        DownloadPingLabel = L("DownloadPing");
        UploadPingLabel = L("UploadPing");
        GoLabel = IsTesting ? "…" : L("SpeedTestGo");
        StatusHint = IsConnected ? L("SpeedHintReady") : L("SpeedHintDisconnected");
        GaugeUnit = Phase switch
        {
            SpeedTestPhase.Upload => L("UploadMbpsShort"),
            SpeedTestPhase.Download => L("DownloadMbpsShort"),
            _ => "Mbps"
        };
    }

    private void OnVpnStatusChanged(object? sender, ConnectionStatus status)
    {
        IsConnected = status.State == ConnectionState.Connected;
        ApplyConnectionState();
        if (!string.IsNullOrWhiteSpace(status.ServerName))
        {
            ServerInfo = status.ServerName;
        }
    }

    private void ApplyConnectionState()
    {
        RefreshLabels();
        if (!IsConnected)
        {
            ResetResults();
            ClientInfo = "—";
            ServerInfo = "—";
            return;
        }

        ServerInfo = _vpn.CurrentStatus.ServerName ?? "VPN";
        _ = RefreshClientInfoAsync();
    }

    private async Task RefreshClientInfoAsync()
    {
        try
        {
            var viaProxy = _vpn.State == ConnectionState.Connected && !_vpn.IsTunMode;
            var ipInfo = await _ipInfo.GetPublicIpAsync(viaProxy);
            ClientInfo = string.IsNullOrWhiteSpace(ipInfo.ip) || ipInfo.ip == "—"
                ? "VPN"
                : $"{ipInfo.ip}";
        }
        catch
        {
            ClientInfo = "VPN";
        }
    }

    [RelayCommand(CanExecute = nameof(CanRunSpeedTest))]
    private async Task RunSpeedTestAsync()
    {
        if (!IsConnected || IsTesting)
        {
            return;
        }

        _testCts?.Cancel();
        _testCts = new CancellationTokenSource();
        var token = _testCts.Token;

        IsTesting = true;
        ShowGoButton = false;
        ShowGauge = true;
        Phase = SpeedTestPhase.Ping;
        GaugeSpeed = 0;
        ResetResults();
        RefreshLabels();

        var progress = new Progress<SpeedTestProgress>(p =>
        {
            Application.Current?.Dispatcher.Invoke(() => ApplyProgress(p), DispatcherPriority.Background);
        });

        try
        {
            var (down, up) = await _speedTest.MeasureSpeedAsync(progress, token);
            DownloadDisplay = FormatMbps(down);
            UploadDisplay = FormatMbps(up);
            GaugeSpeed = up;
            Phase = SpeedTestPhase.Done;
            StatusHint = L("SpeedTestDone");
        }
        catch (OperationCanceledException)
        {
            StatusHint = L("SpeedTestCancelled");
        }
        catch
        {
            StatusHint = L("SpeedTestFailed");
        }
        finally
        {
            IsTesting = false;
            ShowGoButton = true;
            RefreshLabels();
        }
    }

    private bool CanRunSpeedTest() => IsConnected && !IsTesting;

    partial void OnIsConnectedChanged(bool value) => RunSpeedTestCommand.NotifyCanExecuteChanged();
    partial void OnIsTestingChanged(bool value) => RunSpeedTestCommand.NotifyCanExecuteChanged();

    private void ApplyProgress(SpeedTestProgress progress)
    {
        Phase = progress.Phase;
        RefreshLabels();

        switch (progress.Phase)
        {
            case SpeedTestPhase.Ping:
                if (progress.PingMs > 0)
                {
                    IdlePingDisplay = F("PingMsFormat", progress.PingMs);
                    PingDisplay = IdlePingDisplay;
                }
                StatusHint = L("SpeedTestMeasuringPing");
                break;
            case SpeedTestPhase.Download:
                GaugeSpeed = progress.MegabitsPerSecond;
                DownloadDisplay = FormatMbps(progress.MegabitsPerSecond);
                if (progress.PingMs > 0)
                {
                    DownloadPingDisplay = F("PingMsFormat", progress.PingMs);
                }
                else if (IdlePingDisplay != "—")
                {
                    DownloadPingDisplay = IdlePingDisplay;
                }
                StatusHint = L("SpeedTestMeasuringDownload");
                break;
            case SpeedTestPhase.Upload:
                GaugeSpeed = progress.MegabitsPerSecond;
                UploadDisplay = FormatMbps(progress.MegabitsPerSecond);
                if (IdlePingDisplay != "—")
                {
                    UploadPingDisplay = IdlePingDisplay;
                }
                StatusHint = L("SpeedTestMeasuringUpload");
                break;
            case SpeedTestPhase.Done:
                StatusHint = L("SpeedTestDone");
                break;
        }
    }

    private void ResetResults()
    {
        DownloadDisplay = "—";
        UploadDisplay = "—";
        PingDisplay = "—";
        IdlePingDisplay = "—";
        DownloadPingDisplay = "—";
        UploadPingDisplay = "—";
        GaugeSpeed = 0;
    }

    private static string FormatMbps(double mbps) =>
        mbps < 0.05 ? "—" : $"{mbps.ToString("0.00", CultureInfo.CurrentCulture)} Mbps";

    public void Dispose()
    {
        _testCts?.Cancel();
        _vpn.StatusChanged -= OnVpnStatusChanged;
    }
}
