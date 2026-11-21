using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SumraX.App.Converters;
using SumraX.App.Views;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Core.Vpn;
using SumraX.Infrastructure.Vpn;

namespace SumraX.App.ViewModels;

public partial class ServersViewModel : LocalizableViewModelBase
{
    private readonly IServerRepository _servers;
    private readonly IConfigImportService _import;
    private readonly ISubscriptionRefreshService _subRefresh;
    private readonly QrImportService _qrImport;
    private readonly ISpeedTestService _speedTest;
    private readonly ISettingsRepository _settings;
    private readonly IVpnEngine _vpn;

    public ObservableCollection<ServerProfile> Items { get; } = [];
    public ObservableCollection<ServerProfile> FilteredItems { get; } = [];
    public ObservableCollection<ServerCountryGroup> CountryGroups { get; } = [];
    public ObservableCollection<ServerSubscriptionGroup> HomeGroups { get; } = [];

    public event Action? ServerPicked;

    private string? _lastSelectedServerId;
    public string? SelectedServerId => _lastSelectedServerId;
    private Dictionary<string, bool> _homeGroupExpanded = new(StringComparer.Ordinal);
    private Dictionary<string, SubscriptionTrafficInfo> _subscriptionTraffic = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string> _subscriptionRemarks = new(StringComparer.OrdinalIgnoreCase);
    private List<string> _pinnedSubscriptionUrls = [];
    [ObservableProperty] private string _importLink = string.Empty;
    [ObservableProperty] private string _searchQuery = string.Empty;
    [ObservableProperty] private int _sortMode;
    [ObservableProperty] private int _filterMode;
    [ObservableProperty] private bool _isTesting;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _pageTitle = "";
    [ObservableProperty] private string _pasteLabel = "";
    [ObservableProperty] private string _importLabel = "";
    [ObservableProperty] private string _qrLabel = "";
    [ObservableProperty] private string _fileLabel = "";
    [ObservableProperty] private string _manualLabel = "";
    [ObservableProperty] private string _testAllLabel = "";
    [ObservableProperty] private string _useLabel = "";
    [ObservableProperty] private string _importTooltip = "";
    [ObservableProperty] private string _refreshSubsLabel = "";
    [ObservableProperty] private string _connectLabel = "";
    [ObservableProperty] private string _searchLabel = "";
    [ObservableProperty] private string _sortLabel = "";
    [ObservableProperty] private string _filterLabel = "";
    [ObservableProperty] private ServerProfile? _selected;
    [ObservableProperty] private string _pickerTitle = "";
    [ObservableProperty] private string _pickerTabPlaces = "";
    [ObservableProperty] private string _pickerTabFavorites = "";
    [ObservableProperty] private string _pickerTabRecent = "";
    [ObservableProperty] private string _pickerTabSmart = "";
    [ObservableProperty] private string _pickerTabPing = "";
    [ObservableProperty] private string _pickerTabName = "";
    [ObservableProperty] private string _pickerTabLoad = "";
    [ObservableProperty] private string _pickerSortLabel = "";
    [ObservableProperty] private string _activeBadgeLabel = "";
    [ObservableProperty] private string _groupMenuSettingsLabel = "";
    [ObservableProperty] private string _groupMenuRoutingLabel = "";
    [ObservableProperty] private string _groupMenuPinLabel = "";
    [ObservableProperty] private string _groupMenuDeleteLabel = "";

    /// <summary>Order matches SortMode: 0 Smart, 1 Ping, 2 Name, 3 Load.</summary>
    public string[] SortOptions { get; } = ["Smart", "Ping", "Name", "Load"];
    public string[] FilterOptions { get; } = ["All", "Favorites", "Best"];

    public ServersViewModel(
        IServerRepository servers,
        IConfigImportService import,
        ISubscriptionRefreshService subRefresh,
        QrImportService qrImport,
        ISpeedTestService speedTest,
        ISettingsRepository settings,
        IVpnEngine vpn,
        ILocalizationService loc) : base(loc)
    {
        _servers = servers;
        _import = import;
        _subRefresh = subRefresh;
        _qrImport = qrImport;
        _speedTest = speedTest;
        _settings = settings;
        _vpn = vpn;
        CompleteInitialization();
        _ = LoadAsync();
    }

    protected override void OnLanguageChanged()
    {
        PageTitle = L("NavServers");
        PasteLabel = L("Paste");
        ImportLabel = L("Import");
        QrLabel = L("Qr");
        FileLabel = L("File");
        ManualLabel = L("ManualEntry");
        TestAllLabel = L("TestAll");
        UseLabel = L("Use");
        ImportTooltip = L("ImportTooltip");
        RefreshSubsLabel = L("RefreshSubscriptionsNow");
        ConnectLabel = L("Connect");
        SearchLabel = L("SearchServers");
        SortLabel = L("SortBy");
        FilterLabel = L("FilterBy");
        PickerTitle = L("SelectServerLocation");
        PickerTabPlaces = L("PickerTabPlaces");
        PickerTabFavorites = L("PickerTabFavorites");
        PickerTabRecent = L("PickerTabRecent");
        PickerSortLabel = L("PickerSortLabel");
        PickerTabSmart = L("PickerTabSmart");
        PickerTabPing = L("PickerTabPing");
        PickerTabName = L("PickerTabName");
        PickerTabLoad = L("PickerTabLoad");
        ActiveBadgeLabel = L("ActiveBadge");
        GroupMenuSettingsLabel = L("GroupMenuSettings");
        GroupMenuRoutingLabel = L("GroupMenuRouting");
        GroupMenuPinLabel = L("GroupMenuPin");
        GroupMenuDeleteLabel = L("GroupMenuDelete");
    }

    public Task RefreshListAsync() => LoadAsync();

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSortModeChanged(int value) => ApplyFilter();
    partial void OnFilterModeChanged(int value) => ApplyFilter();

    private void ApplyFilter()
    {
        FilteredItems.Clear();
        IEnumerable<ServerProfile> query = Items;

        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            query = query.Where(s =>
                s.Name.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                s.CountryName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                s.Address.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
        }

        query = FilterMode switch
        {
            1 => query.Where(s => s.IsFavorite),
            2 => query,
            _ => query
        };

        query = FilterMode switch
        {
            2 => query.OrderByDescending(s => Items.IndexOf(s)),
            _ => SortMode switch
            {
                1 => query.OrderBy(s => s.PingMs <= 0 ? int.MaxValue : s.PingMs),
                2 => query.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase),
                3 => query.OrderByDescending(s => s.LoadPercent),
                _ => SmartProfileRanker.OrderBySmart(query, preferFavorite: true)
            }
        };

        foreach (var s in query)
        {
            FilteredItems.Add(s);
        }

        RebuildCountryGroups();
        RebuildHomeGroups();
    }

    private void RebuildHomeGroups()
    {
        HomeGroups.Clear();
        var grouped = Items
            .GroupBy(NormalizeGroupKey)
            .OrderByDescending(g => _pinnedSubscriptionUrls.Contains(g.Key, StringComparer.OrdinalIgnoreCase))
            .ThenBy(g => ResolveGroupTitle(g.Key), StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            var key = group.Key;
            var servers = group.OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            var title = ResolveGroupTitle(key);
            var subtitle = ResolveGroupSubtitle(key, _subscriptionTraffic);
            var expanded = !_homeGroupExpanded.TryGetValue(key, out var state) || state;

            HomeGroups.Add(new ServerSubscriptionGroup
            {
                GroupKey = key,
                Title = title,
                Subtitle = subtitle,
                Servers = new ObservableCollection<ServerProfile>(servers),
                IsExpanded = expanded
            });
        }
    }

    private static string NormalizeGroupKey(ServerProfile server)
    {
        var url = SubscriptionUrlNormalizer.Normalize(server.SubscriptionUrl ?? "");
        if (string.IsNullOrWhiteSpace(url) || !SubscriptionUrlNormalizer.IsSubscriptionUrl(url))
        {
            return "";
        }

        return url;
    }

    private string ResolveGroupTitle(string subscriptionUrl)
    {
        if (string.IsNullOrWhiteSpace(subscriptionUrl))
        {
            return L("DefaultServerGroup");
        }

        if (_subscriptionRemarks.TryGetValue(subscriptionUrl, out var remark) && !string.IsNullOrWhiteSpace(remark))
        {
            return remark.Trim();
        }

        if (Uri.TryCreate(subscriptionUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host;
        }

        return subscriptionUrl.Length > 36 ? subscriptionUrl[..36] + "…" : subscriptionUrl;
    }

    private static string ResolveGroupSubtitle(string subscriptionUrl, IReadOnlyDictionary<string, SubscriptionTrafficInfo> traffic)
    {
        if (string.IsNullOrWhiteSpace(subscriptionUrl))
        {
            return "";
        }

        if (traffic.TryGetValue(subscriptionUrl, out var info) && info.FetchedUtc != default)
        {
            return info.FetchedUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
        }

        return "";
    }

    private void RebuildCountryGroups()
    {
        CountryGroups.Clear();
        foreach (var group in FilteredItems
                     .GroupBy(s => string.IsNullOrWhiteSpace(s.CountryName) ? "Unknown" : s.CountryName)
                     .OrderBy(g => g.Key))
        {
            var first = group.First();
            var servers = group.OrderBy(s => s.PingMs <= 0 ? int.MaxValue : s.PingMs).ToList();
            var isActive = !string.IsNullOrWhiteSpace(_lastSelectedServerId) &&
                           servers.Any(s => s.Id == _lastSelectedServerId);

            CountryGroups.Add(new ServerCountryGroup
            {
                CountryName = group.Key,
                CountryCode = first.CountryCode,
                FlagEmoji = CountryFlagConverter.ToFlag(first.CountryCode),
                Servers = new ObservableCollection<ServerProfile>(servers),
                IsActive = isActive,
                IsExpanded = isActive
            });
        }
    }

    [RelayCommand]
    private void SetPickerSort(string mode)
    {
        if (int.TryParse(mode, out var idx))
        {
            SortMode = idx;
        }
    }

    [RelayCommand]
    private void SetPickerFilter(string mode)
    {
        if (int.TryParse(mode, out var idx))
        {
            FilterMode = idx;
        }
    }

    [RelayCommand]
    private void ToggleCountryGroup(ServerCountryGroup? group)
    {
        if (group != null)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    [RelayCommand]
    private void ToggleHomeGroup(ServerSubscriptionGroup? group)
    {
        if (group == null)
        {
            return;
        }

        group.IsExpanded = !group.IsExpanded;
        _homeGroupExpanded[group.GroupKey] = group.IsExpanded;
    }

    [RelayCommand]
    private async Task RefreshHomeGroupAsync(string? groupKey)
    {
        groupKey ??= "";
        IsTesting = true;
        StatusMessage = L("LoadingSubscription");
        try
        {
            if (!string.IsNullOrWhiteSpace(groupKey))
            {
                var refresh = await _subRefresh.RefreshUrlAsync(groupKey);
                StatusMessage = refresh.Success
                    ? F("SubRefreshCompleted", refresh.UpdatedServers, refresh.RemovedServers, refresh.Skipped)
                    : refresh.Errors.FirstOrDefault() ?? L("SubscriptionImportFailed");
            }
            else
            {
                await RefreshSubscriptionsAsync();
                return;
            }

            await LoadAsync();
            App.HostApp.Services.GetRequiredService<HomeViewModel>().RefreshSelectedServer();
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private void OpenGroupSettings(string? groupKey)
    {
        if (string.IsNullOrWhiteSpace(groupKey))
        {
            return;
        }

        App.HostApp.Services.GetRequiredService<MainViewModel>().OpenSettingsSubscriptions(groupKey);
    }

    [RelayCommand]
    private void OpenGroupRouting()
    {
        App.HostApp.Services.GetRequiredService<MainViewModel>().OpenSettingsRouting();
    }

    [RelayCommand]
    private async Task TogglePinHomeGroupAsync(string? groupKey)
    {
        groupKey ??= "";
        var settings = await _settings.GetAsync();
        var pinned = settings.PinnedSubscriptionUrls.ToList();
        if (pinned.Contains(groupKey, StringComparer.OrdinalIgnoreCase))
        {
            pinned.RemoveAll(u => string.Equals(u, groupKey, StringComparison.OrdinalIgnoreCase));
        }
        else if (!string.IsNullOrWhiteSpace(groupKey))
        {
            pinned.Insert(0, groupKey);
        }

        settings.PinnedSubscriptionUrls = pinned;
        await _settings.SaveAsync(settings);
        _pinnedSubscriptionUrls = pinned;
        RebuildHomeGroups();
    }

    [RelayCommand]
    private async Task DeleteHomeGroupAsync(string? groupKey)
    {
        groupKey ??= "";
        var settings = await _settings.GetAsync();
        if (settings.ConfirmRemove)
        {
            var result = MessageBox.Show(
                L("GroupMenuDeleteConfirm"),
                L("AppTitle"),
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var victims = Items
            .Where(s => NormalizeGroupKey(s) == groupKey)
            .ToList();
        foreach (var server in victims)
        {
            await _servers.DeleteAsync(server.Id);
        }

        if (!string.IsNullOrWhiteSpace(groupKey))
        {
            await _subRefresh.UnregisterSubscriptionUrlAsync(groupKey);
            settings.SubscriptionUrls.RemoveAll(u => string.Equals(u, groupKey, StringComparison.OrdinalIgnoreCase));
            settings.SubscriptionRemarks.Remove(groupKey);
            settings.PinnedSubscriptionUrls.RemoveAll(u => string.Equals(u, groupKey, StringComparison.OrdinalIgnoreCase));
            await _settings.SaveAsync(settings);
        }

        await LoadAsync();
        App.HostApp.Services.GetRequiredService<HomeViewModel>().RefreshSelectedServer();
    }

    [RelayCommand]
    private async Task PingHomeGroupAsync(string? groupKey)
    {
        groupKey ??= "";
        var targets = Items.Where(s => NormalizeGroupKey(s) == (groupKey ?? "")).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        IsTesting = true;
        StatusMessage = L("TestingServers");
        try
        {
            await _speedTest.MeasureAllPingsAsync(targets);
            foreach (var server in targets)
            {
                await _servers.UpdateMetricsAsync(server.Id, server.PingMs, server.LoadPercent, server.DownloadMbps, server.UploadMbps);
            }

            await LoadAsync();
            StatusMessage = L("PingTestCompleted");
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task PickServerAsync(ServerProfile? server)
    {
        await SelectServerAsync(server);
        ServerPicked?.Invoke();
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var settings = await _settings.GetAsync();
        _lastSelectedServerId = settings.LastSelectedServerId;
        _subscriptionTraffic = settings.SubscriptionTraffic;
        _subscriptionRemarks = settings.SubscriptionRemarks;
        _pinnedSubscriptionUrls = settings.PinnedSubscriptionUrls.ToList();
        OnPropertyChanged(nameof(SelectedServerId));

        Items.Clear();
        foreach (var s in await _servers.GetAllAsync())
        {
            if (!CoreSelector.IsSupported(s.Protocol))
            {
                continue;
            }

            Items.Add(s);
        }
        ApplyFilter();
    }

    [RelayCommand]
    private async Task ConnectToServerAsync(ServerProfile? server)
    {
        if (server == null)
        {
            return;
        }

        await SelectServerAsync(server);
        await _vpn.ConnectAsync(server);
    }

    [RelayCommand]
    private async Task SelectServerAsync(ServerProfile? server)
    {
        if (server == null)
        {
            return;
        }

        var settings = await _settings.GetAsync();
        settings.LastSelectedServerId = server.Id;
        await _settings.SaveAsync(settings);
        _lastSelectedServerId = server.Id;
        OnPropertyChanged(nameof(SelectedServerId));
        Selected = server;
        StatusMessage = F("SelectedServer", server.Name);
        App.HostApp.Services.GetRequiredService<HomeViewModel>().RefreshSelectedServer();
        RebuildCountryGroups();
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(ServerProfile? server)
    {
        if (server == null)
        {
            return;
        }

        server.IsFavorite = !server.IsFavorite;
        await _servers.SetFavoriteAsync(server.Id, server.IsFavorite);
        await LoadAsync();
        ApplyFilter();
    }

    [RelayCommand]
    private async Task DeleteServerAsync(ServerProfile? server)
    {
        if (server == null)
        {
            return;
        }

        await _servers.DeleteAsync(server.Id);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ImportLinkAsync()
    {
        var raw = ImportLink.Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            StatusMessage = L("PasteLinkRequired");
            return;
        }

        var lines = raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length > 1)
        {
            await BatchImportAsync(lines);
            return;
        }

        var input = SubscriptionUrlNormalizer.Normalize(raw);
        if (string.IsNullOrWhiteSpace(input))
        {
            StatusMessage = L("PasteLinkRequired");
            return;
        }

        if (SubscriptionUrlNormalizer.IsSubscriptionUrl(input) ||
            input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            await ImportSubscriptionAsync(input);
            return;
        }

        if (CryptLinkDecryptor.IsCryptLink(raw))
        {
            if (CryptLinkDecryptor.TryDecrypt(raw, out var decrypted))
            {
                if (SubscriptionUrlNormalizer.IsSubscriptionUrl(decrypted) ||
                    decrypted.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    decrypted.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    await ImportSubscriptionAsync(decrypted);
                }
                else if (SubscriptionUrlNormalizer.IsProtocolLink(decrypted))
                {
                    var profile = await _import.ImportFromLinkAsync(decrypted);
                    if (profile == null)
                    {
                        StatusMessage = L("ParseLinkFailed");
                        return;
                    }

                    await _servers.SaveAsync(profile);
                    ImportLink = string.Empty;
                    StatusMessage = F("ImportedServer", profile.Name);
                    await LoadAsync();
                }
                else
                {
                    StatusMessage = L("InvalidSubscriptionFormat");
                }

                return;
            }

            StatusMessage = L("CryptLinkDecryptFailed");
            return;
        }

        if (SubscriptionUrlNormalizer.IsProtocolLink(input))
        {
            var profile = await _import.ImportFromLinkAsync(input);
            if (profile == null)
            {
                StatusMessage = L("ParseLinkFailed");
                return;
            }

            await _servers.SaveAsync(profile);
            ImportLink = string.Empty;
            StatusMessage = F("ImportedServer", profile.Name);
            await LoadAsync();
            return;
        }

        StatusMessage = L("InvalidSubscriptionFormat");
    }

    private async Task BatchImportAsync(IReadOnlyList<string> lines)
    {
        IsTesting = true;
        var imported = 0;
        var skipped = 0;
        var errors = 0;

        try
        {
            foreach (var line in lines)
            {
                var raw = line.Trim();
                if (raw.Length == 0)
                {
                    continue;
                }

                var input = SubscriptionUrlNormalizer.Normalize(raw);
                if (CryptLinkDecryptor.IsCryptLink(raw))
                {
                    if (!CryptLinkDecryptor.TryDecrypt(raw, out input))
                    {
                        errors++;
                        continue;
                    }
                }

                if (SubscriptionUrlNormalizer.IsSubscriptionUrl(input) ||
                    input.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    input.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _import.ImportFromSubscriptionUrlAsync(input);
                    if (result.Success)
                    {
                        imported += result.Servers.Count;
                        skipped += result.Skipped;
                        foreach (var server in result.Servers)
                        {
                            await _servers.SaveAsync(server);
                        }
                    }
                    else
                    {
                        errors++;
                    }

                    continue;
                }

                if (SubscriptionUrlNormalizer.IsProtocolLink(input))
                {
                    var profile = await _import.ImportFromLinkAsync(input);
                    if (profile == null)
                    {
                        errors++;
                        continue;
                    }

                    await _servers.SaveAsync(profile);
                    imported++;
                    continue;
                }

                errors++;
            }

            ImportLink = string.Empty;
            StatusMessage = F("BatchImportResult", imported, skipped, errors);
            await LoadAsync();
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task PasteAndImportAsync()
    {
        if (Clipboard.ContainsText())
        {
            ImportLink = Clipboard.GetText().Trim();
        }

        await ImportLinkAsync();
    }

    [RelayCommand]
    private async Task ImportSubscriptionAsync(string? url = null)
    {
        url = SubscriptionUrlNormalizer.Normalize(url ?? ImportLink);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        IsTesting = true;
        StatusMessage = L("LoadingSubscription");
        try
        {
            await _subRefresh.RegisterSubscriptionUrlAsync(url);
            var refresh = await _subRefresh.RefreshUrlAsync(url);
            if (!refresh.Success)
            {
                StatusMessage = refresh.Errors.FirstOrDefault() ?? L("SubscriptionImportFailed");
                return;
            }

            ImportLink = string.Empty;
            StatusMessage = F("SubRefreshCompleted", refresh.UpdatedServers, refresh.RemovedServers, refresh.Skipped);
            await LoadAsync();
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task RefreshSubscriptionsAsync()
    {
        IsTesting = true;
        StatusMessage = L("LoadingSubscription");
        try
        {
            var urls = await _subRefresh.GetSubscriptionUrlsAsync();
            if (urls.Count == 0)
            {
                StatusMessage = L("SubRefreshNone");
                return;
            }

            var result = await _subRefresh.RefreshAllAsync();
            if (!result.Success)
            {
                StatusMessage = F("SubRefreshFailed", string.Join("; ", result.Errors));
                return;
            }

            StatusMessage = F("SubRefreshCompleted", result.UpdatedServers, result.RemovedServers, result.Skipped);
            await LoadAsync();
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task ImportFileAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = L("ConfigFileFilter")
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        var profile = await _import.ImportFromFileAsync(dlg.FileName);
        if (profile == null)
        {
            StatusMessage = L("ParseFileFailed");
            return;
        }

        await _servers.SaveAsync(profile);
        StatusMessage = F("ImportedServer", profile.Name);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ImportQrAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = L("ImageFileFilter")
        };
        if (dlg.ShowDialog() != true)
        {
            return;
        }

        var profile = await _qrImport.ImportFromImageFileAsync(dlg.FileName);
        if (profile == null)
        {
            StatusMessage = L("QrNotFound");
            return;
        }

        await _servers.SaveAsync(profile);
        StatusMessage = F("ImportedFromQr", profile.Name);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task AddManualAsync()
    {
        var loc = App.HostApp.Services.GetRequiredService<ILocalizationService>();
        var window = new ManualServerWindow(loc) { Owner = Application.Current.MainWindow };
        if (window.ShowDialog() != true || window.Result == null)
        {
            return;
        }

        await _servers.SaveAsync(window.Result);
        StatusMessage = F("AddedServer", window.Result.Name);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task TestAllAsync()
    {
        IsTesting = true;
        StatusMessage = L("TestingServers");
        try
        {
            var settings = await _settings.GetAsync();
            var servers = Items.ToList();
            await _speedTest.MeasureAllPingsAsync(servers);

            foreach (var server in servers)
            {
                await _servers.UpdateMetricsAsync(server.Id, server.PingMs, server.LoadPercent, server.DownloadMbps, server.UploadMbps);
            }

            if (settings.AutoRemoveInvalidAfterTest)
            {
                foreach (var dead in servers.Where(s => s.PingMs <= 0).ToList())
                {
                    await _servers.DeleteAsync(dead.Id);
                }
            }

            await LoadAsync();

            if (settings.AutoSortAfterTest)
            {
                SortMode = 1; // Ping (explicit user-visible order after a latency test)
                ApplyFilter();
            }

            StatusMessage = L("PingTestCompleted");
        }
        finally
        {
            IsTesting = false;
        }
    }
}
