using SumraX.Core.Interfaces;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

public sealed class SubscriptionRefreshService : ISubscriptionRefreshService, IDisposable
{
    private readonly ISettingsRepository _settings;
    private readonly IServerRepository _servers;
    private readonly IConfigImportService _import;
    private readonly ILocalizationService _loc;
    private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

    private Timer? _timer;
    private int _refreshRunning;

    public SubscriptionRefreshService(
        ISettingsRepository settings,
        IServerRepository servers,
        IConfigImportService import,
        ILocalizationService loc)
    {
        _settings = settings;
        _servers = servers;
        _import = import;
        _loc = loc;
    }

    public async Task<IReadOnlyList<string>> GetSubscriptionUrlsAsync()
    {
        var settings = await _settings.GetAsync();
        if (settings.SubscriptionUrls.Count == 0)
        {
            await SyncUrlsFromServersAsync(settings);
        }

        return settings.SubscriptionUrls.AsReadOnly();
    }

    public async Task RegisterSubscriptionUrlAsync(string url)
    {
        url = SubscriptionUrlNormalizer.Normalize(url);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var settings = await _settings.GetAsync();
        if (settings.SubscriptionUrls.Any(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        settings.SubscriptionUrls.Add(url);
        await _settings.SaveAsync(settings);
    }

    public async Task UnregisterSubscriptionUrlAsync(string url)
    {
        url = SubscriptionUrlNormalizer.Normalize(url);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        var settings = await _settings.GetAsync();
        settings.SubscriptionUrls.RemoveAll(u => string.Equals(u, url, StringComparison.OrdinalIgnoreCase));
        await _settings.SaveAsync(settings);
    }

    public async Task<SubscriptionRefreshResult> RefreshUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        url = SubscriptionUrlNormalizer.Normalize(url);
        if (string.IsNullOrWhiteSpace(url))
        {
            return new SubscriptionRefreshResult { Errors = [_loc.Get("SubInvalidUrl")] };
        }

        var urlLabel = DescribeUrl(url);
        var import = await _import.ImportFromSubscriptionUrlAsync(url, cancellationToken);
        if (!import.Success)
        {
            Log.Warn($"Subscription refresh failed for {urlLabel}: {import.Error ?? "unknown"}");
            return new SubscriptionRefreshResult
            {
                Errors = new[] { import.Error ?? _loc.Get("SubscriptionImportFailed") }
            };
        }

        Log.Info($"Subscription {urlLabel}: fetched {import.Servers.Count} servers");

        await SaveTrafficInfoAsync(url, import.Traffic);

        var settings = await _settings.GetAsync();
        if (!string.IsNullOrWhiteSpace(import.ProfileTitle))
        {
            settings.SubscriptionRemarks[url] = import.ProfileTitle.Trim();
            await _settings.SaveAsync(settings);
        }
        var purge = settings.PurgeOldProfilesOnSubscriptionUpdate;
        var all = (await _servers.GetAllAsync()).ToList();

        var bySub = all
            .Where(s => string.Equals(s.SubscriptionUrl, url, StringComparison.OrdinalIgnoreCase))
            .GroupBy(ProfileKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Untagged profiles with the same fingerprint — reclaim on update so they don't pile up.
        var orphans = all
            .Where(s => string.IsNullOrWhiteSpace(s.SubscriptionUrl))
            .GroupBy(ProfileKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Cross-subscription duplicates: same endpoint (ignore remark/name).
        var globalEndpoints = settings.IgnoreDuplicateServers
            ? all
                .Where(s => !string.Equals(s.SubscriptionUrl, url, StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrWhiteSpace(s.SubscriptionUrl))
                .Select(EndpointKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : [];

        var newKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var skippedDupes = 0;
        var serversToSave = new List<ServerProfile>();
        foreach (var server in import.Servers)
        {
            server.SubscriptionUrl = url;
            var profileKey = ProfileKey(server);
            var endpointKey = EndpointKey(server);
            if (settings.IgnoreDuplicateServers && globalEndpoints.Contains(endpointKey))
            {
                skippedDupes++;
                continue;
            }

            newKeys.Add(profileKey);
            if (bySub.TryGetValue(profileKey, out var old) || orphans.TryGetValue(profileKey, out old))
            {
                server.Id = old.Id;
                server.IsFavorite = old.IsFavorite;
                server.PingMs = old.PingMs;
                server.LoadPercent = old.LoadPercent;
                server.DownloadMbps = old.DownloadMbps;
                server.UploadMbps = old.UploadMbps;
                server.LastUsedAt = old.LastUsedAt;
            }

            serversToSave.Add(server);
        }

        var removed = 0;
        if (purge)
        {
            // Drop profiles from this subscription that are no longer in the feed.
            foreach (var old in bySub.Values.Where(s => !newKeys.Contains(ProfileKey(s))))
            {
                await _servers.DeleteAsync(old.Id);
                removed++;
            }

            // Drop untagged duplicates that match the new feed (reclaimed ones keep Id and are overwritten).
            foreach (var orphan in orphans.Values)
            {
                var key = ProfileKey(orphan);
                if (!newKeys.Contains(key))
                {
                    continue;
                }

                // If we reclaimed this orphan into serversToSave, do not delete.
                if (serversToSave.Any(s => s.Id == orphan.Id))
                {
                    continue;
                }

                await _servers.DeleteAsync(orphan.Id);
                removed++;
            }

            // Extra copies of the same profile key under this subscription URL.
            var seenIds = serversToSave.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var dup in all.Where(s =>
                         string.Equals(s.SubscriptionUrl, url, StringComparison.OrdinalIgnoreCase)
                         && newKeys.Contains(ProfileKey(s))
                         && !seenIds.Contains(s.Id)))
            {
                await _servers.DeleteAsync(dup.Id);
                removed++;
            }
        }

        var saved = await _servers.SaveManyAsync(serversToSave);
        Log.Info($"Subscription {urlLabel}: saved={saved}, removed={removed}, skipped={import.Skipped + skippedDupes}");
        return new SubscriptionRefreshResult
        {
            UpdatedServers = saved,
            RemovedServers = removed,
            Skipped = import.Skipped + skippedDupes
        };
    }

    private async Task SaveTrafficInfoAsync(string url, SubscriptionTrafficInfo? traffic)
    {
        if (traffic == null)
        {
            return;
        }

        traffic.Url = url;
        var settings = await _settings.GetAsync();
        settings.SubscriptionTraffic[url] = traffic;
        await _settings.SaveAsync(settings);
    }

    public async Task<SubscriptionRefreshResult> RefreshAllAsync(CancellationToken cancellationToken = default)
    {
        var urls = await GetSubscriptionUrlsAsync();
        var aggregate = new SubscriptionRefreshResult();
        var errors = new List<string>();

        foreach (var url in urls)
        {
            var one = await RefreshUrlAsync(url, cancellationToken);
            aggregate.UpdatedServers += one.UpdatedServers;
            aggregate.RemovedServers += one.RemovedServers;
            aggregate.Skipped += one.Skipped;
            errors.AddRange(one.Errors);
        }

        var settings = await _settings.GetAsync();
        if (settings.PurgeOldProfilesOnSubscriptionUpdate)
        {
            var knownUrls = new HashSet<string>(
                settings.SubscriptionUrls.Select(SubscriptionUrlNormalizer.Normalize),
                StringComparer.OrdinalIgnoreCase);
            foreach (var orphanSub in (await _servers.GetAllAsync())
                         .Where(s => !string.IsNullOrWhiteSpace(s.SubscriptionUrl)
                                     && !knownUrls.Contains(SubscriptionUrlNormalizer.Normalize(s.SubscriptionUrl))))
            {
                await _servers.DeleteAsync(orphanSub.Id);
                aggregate.RemovedServers++;
            }
        }

        // Stamp only when at least one feed actually refreshed, otherwise keep the previous
        // timestamp so the periodic timer retries as soon as the tunnel is back and the
        // UI never claims "updated" after a silent failure.
        var succeeded = urls.Count > 0 && errors.Count == 0;
        if (succeeded)
        {
            settings.LastSubscriptionRefreshUtc = DateTime.UtcNow;
            await _settings.SaveAsync(settings);
        }
        else if (errors.Count > 0)
        {
            Log.Warn($"Subscription refresh finished with {errors.Count} error(s), timestamp not advanced: {string.Join(" | ", errors)}");
        }

        return new SubscriptionRefreshResult
        {
            UpdatedServers = aggregate.UpdatedServers,
            RemovedServers = aggregate.RemovedServers,
            Skipped = aggregate.Skipped,
            Errors = errors
        };
    }

    public async Task RefreshIfDueAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _refreshRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var settings = await _settings.GetAsync();
            if (!settings.AutoRefreshSubscriptions)
            {
                return;
            }

            var urls = await GetSubscriptionUrlsAsync();
            if (urls.Count == 0)
            {
                return;
            }

            var minutes = settings.SubscriptionRefreshIntervalMinutes > 0
                ? settings.SubscriptionRefreshIntervalMinutes
                : Math.Max(1, settings.SubscriptionRefreshIntervalHours) * 60;
            var interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
            if (settings.LastSubscriptionRefreshUtc != default &&
                DateTime.UtcNow - settings.LastSubscriptionRefreshUtc < interval)
            {
                return;
            }

            await RefreshAllAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshRunning, 0);
        }
    }

    public async Task RefreshOnStartupAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _refreshRunning, 1, 0) != 0)
        {
            return;
        }

        try
        {
            var settings = await _settings.GetAsync();
            if (!settings.UpdateSubscriptionsOnStart)
            {
                return;
            }

            var urls = await GetSubscriptionUrlsAsync();
            if (urls.Count == 0)
            {
                return;
            }

            await RefreshAllAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Exchange(ref _refreshRunning, 0);
        }
    }

    public void StartPeriodicRefresh()
    {
        _timer?.Dispose();
        // Tick often enough that a due refresh happens within ~2 minutes of its deadline
        // (re-pulls the feed roughly every 20 minutes while its core is up).
        _timer = new Timer(_ => _ = RefreshIfDueAsync(), null, TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(2));
    }

    public void Dispose() => _timer?.Dispose();

    /// <summary>
    /// Non-sensitive label for logs: host plus a short hash instead of the secret token.
    /// </summary>
    private static string DescribeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "<invalid>";
        }

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(url)))[..8];
        return $"{uri.Scheme}://{uri.Host}/#{hash}";
    }

    private async Task SyncUrlsFromServersAsync(AppSettings settings)
    {
        var fromServers = (await _servers.GetAllAsync())
            .Select(s => s.SubscriptionUrl)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(SubscriptionUrlNormalizer.Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (fromServers.Count == 0)
        {
            return;
        }

        settings.SubscriptionUrls = fromServers;
        await _settings.SaveAsync(settings);
    }

    /// <summary>
    /// Identity for subscription update / purge: keep differently named entries even if endpoint matches.
    /// </summary>
    private static string ProfileKey(ServerProfile s) =>
        $"{s.Protocol}|{s.Address}|{s.Port}|{s.UserId}|{s.Password}|{s.Network}|{s.Path}|{s.Host}|{s.Sni}|{s.Name}";

    /// <summary>
    /// Endpoint fingerprint for cross-subscription duplicate skipping (remark ignored).
    /// </summary>
    private static string EndpointKey(ServerProfile s) =>
        $"{s.Protocol}|{s.Address}|{s.Port}|{s.UserId}|{s.Password}|{s.Network}|{s.Path}|{s.Host}|{s.Sni}";
}
