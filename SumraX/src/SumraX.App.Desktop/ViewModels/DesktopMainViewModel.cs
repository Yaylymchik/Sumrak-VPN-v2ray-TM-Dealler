using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Vpn;

namespace SumraX.App.Desktop.ViewModels;

public partial class DesktopMainViewModel : ObservableObject
{
    private readonly IVpnEngine _vpn;
    private readonly IServerRepository _servers;
    private readonly ISettingsRepository _settings;
    private readonly IConfigImportService _import;
    private readonly ILocalizationService _loc;
    private ServerProfile? _activeServer;

    public ObservableCollection<ServerProfile> Servers { get; } = [];

    [ObservableProperty] private string _title = "Sumrak VPN";
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _serverName = "—";
    [ObservableProperty] private string _connectLabel = "Connect";
    [ObservableProperty] private string _importLink = "";
    [ObservableProperty] private string _importStatus = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _useSystemProxy = true;
    [ObservableProperty] private ServerProfile? _selectedServer;

    public DesktopMainViewModel(
        IVpnEngine vpn,
        IServerRepository servers,
        ISettingsRepository settings,
        IConfigImportService import,
        ILocalizationService loc)
    {
        _vpn = vpn;
        _servers = servers;
        _settings = settings;
        _import = import;
        _loc = loc;
        _vpn.StatusChanged += (_, status) => ApplyStatus(status);
        _loc.LanguageChanged += (_, _) => RefreshLabels();
        RefreshLabels();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var settings = await _settings.GetAsync();
        UseSystemProxy = settings.UseSystemProxy;
        await RefreshServersAsync();
        ApplyStatus(_vpn.CurrentStatus);
    }

    private void RefreshLabels()
    {
        Title = _loc.Get("AppTitle");
        ConnectLabel = _vpn.State == ConnectionState.Connected ? _loc.Get("Disconnect") : _loc.Get("Connect");
        StatusText = _vpn.State switch
        {
            ConnectionState.Connected => _loc.Get("ProtectedStatus"),
            ConnectionState.Connecting => _loc.Get("Connecting"),
            ConnectionState.Disconnected => _loc.Get("UnprotectedStatus"),
            ConnectionState.Error => _loc.Get("AiStateError"),
            _ => _vpn.State.ToString()
        };
    }

    private void ApplyStatus(ConnectionStatus status)
    {
        RefreshLabels();
        StatusText = string.IsNullOrWhiteSpace(status.ErrorMessage)
            ? ConnectLabel
            : status.ErrorMessage;
        ServerName = status.ServerName ?? _activeServer?.Name ?? "—";
    }

    [RelayCommand]
    private async Task RefreshServersAsync()
    {
        Servers.Clear();
        foreach (var server in await _servers.GetAllAsync())
        {
            if (CoreSelector.IsSupported(server.Protocol))
            {
                Servers.Add(server);
            }
        }

        var settings = await _settings.GetAsync();
        _activeServer = !string.IsNullOrWhiteSpace(settings.LastSelectedServerId)
            ? Servers.FirstOrDefault(s => s.Id == settings.LastSelectedServerId) ?? Servers.FirstOrDefault()
            : Servers.FirstOrDefault();
        SelectedServer = _activeServer;
        ServerName = _activeServer?.Name ?? "—";
    }

    [RelayCommand]
    private async Task ToggleConnectionAsync()
    {
        if (_vpn.State == ConnectionState.Connected || _vpn.State == ConnectionState.Connecting)
        {
            IsBusy = true;
            try
            {
                await _vpn.DisconnectAsync();
            }
            finally
            {
                IsBusy = false;
            }

            return;
        }

        var server = SelectedServer ?? _activeServer;
        if (server == null)
        {
            ImportStatus = _loc.Get("NoServers");
            return;
        }

        _activeServer = server;
        var settings = await _settings.GetAsync();
        settings.LastSelectedServerId = server.Id;
        await _settings.SaveAsync(settings);

        IsBusy = true;
        try
        {
            await _vpn.ConnectAsync(server);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ImportLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(ImportLink))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var server = await _import.ImportFromLinkAsync(ImportLink.Trim());
            if (server == null)
            {
                ImportStatus = _loc.Get("SubscriptionImportFailed");
                return;
            }

            await _servers.SaveAsync(server);
            ImportLink = "";
            ImportStatus = _loc.Format("ImportedServer", server.Name);
            await RefreshServersAsync();
            SelectedServer = server;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        var settings = await _settings.GetAsync();
        settings.UseSystemProxy = UseSystemProxy;
        settings.UseTunMode = false;
        await _settings.SaveAsync(settings);
        ImportStatus = _loc.Get("SettingsSaved");
    }

    partial void OnSelectedServerChanged(ServerProfile? value)
    {
        if (value != null)
        {
            ServerName = value.Name;
        }
    }
}
