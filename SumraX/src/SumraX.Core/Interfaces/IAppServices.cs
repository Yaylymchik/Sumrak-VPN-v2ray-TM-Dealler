using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Core.Interfaces;

public interface IVpnEngine
{
    ConnectionState State { get; }
    ConnectionStatus CurrentStatus { get; }
    bool IsTunMode { get; }
    event EventHandler<ConnectionStatus>? StatusChanged;
    Task ConnectAsync(ServerProfile server, CancellationToken cancellationToken = default);
    Task DisconnectAsync(CancellationToken cancellationToken = default);
    Task<bool> IsCoreRunningAsync();
}

public interface IServerRepository
{
    Task<IReadOnlyList<ServerProfile>> GetAllAsync();
    Task<ServerProfile?> GetByIdAsync(string id);
    Task SaveAsync(ServerProfile server);
    Task DeleteAsync(string id);
    Task SetFavoriteAsync(string id, bool favorite);
    Task UpdateMetricsAsync(string id, int pingMs, double load, double download, double upload);
    Task<int> SaveManyAsync(IEnumerable<ServerProfile> servers);
}

public interface ISettingsRepository
{
    Task<AppSettings> GetAsync();
    Task SaveAsync(AppSettings settings);
}

public interface IUserProfileRepository
{
    Task<UserProfile> GetAsync();
    Task SaveAsync(UserProfile profile);
}

public interface IConfigImportService
{
    Task<ServerProfile?> ImportFromLinkAsync(string link);
    Task<ServerProfile?> ImportFromJsonAsync(string json);
    Task<ServerProfile?> ImportFromFileAsync(string filePath);
    Task<SubscriptionImportResult> ImportFromSubscriptionUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<SubscriptionImportResult> ImportFromSubscriptionTextAsync(string text);
}

public interface ISubscriptionRefreshService
{
    Task<IReadOnlyList<string>> GetSubscriptionUrlsAsync();
    Task RegisterSubscriptionUrlAsync(string url);
    Task UnregisterSubscriptionUrlAsync(string url);
    Task<SubscriptionRefreshResult> RefreshUrlAsync(string url, CancellationToken cancellationToken = default);
    Task<SubscriptionRefreshResult> RefreshAllAsync(CancellationToken cancellationToken = default);
    Task RefreshIfDueAsync(CancellationToken cancellationToken = default);
    Task RefreshOnStartupAsync(CancellationToken cancellationToken = default);
    void StartPeriodicRefresh();
}

public sealed class SubscriptionRefreshResult
{
    public int UpdatedServers { get; set; }
    public int RemovedServers { get; set; }
    public int Skipped { get; set; }
    public IReadOnlyList<string> Errors { get; init; } = [];
    public bool Success => Errors.Count == 0;
}

public sealed class SubscriptionImportResult
{
    public IReadOnlyList<ServerProfile> Servers { get; init; } = [];
    public int Skipped { get; init; }
    public string? Error { get; init; }
    public SubscriptionTrafficInfo? Traffic { get; init; }
    public string? ProfileTitle { get; init; }
    public bool Success => Error == null && Servers.Count > 0;
}

public interface IDiagnosticsService
{
    Task<DiagnosticReport> RunAsync(CancellationToken cancellationToken = default);
}

public interface IDebugExportService
{
    Task<string> ExportBundleAsync(CancellationToken cancellationToken = default);
}

public interface ISpeedTestService
{
    Task<int> MeasurePingAsync(ServerProfile server, CancellationToken cancellationToken = default);
    /// <summary>Full batch ping (parallel real ping per server when Proxy/ProxyHead).</summary>
    Task MeasureAllPingsAsync(IReadOnlyList<ServerProfile> servers, CancellationToken cancellationToken = default);
    Task<int> MeasureProxyPingAsync(CancellationToken cancellationToken = default);
    Task<(double download, double upload)> MeasureSpeedAsync(
        IProgress<SpeedTestProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface ISecureStorage
{
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    Task SaveSecureAsync(string key, string value);
    Task<string?> LoadSecureAsync(string key);
}

public interface IBackupService
{
    Task BackupAsync();
    Task RestoreLatestAsync();
}

public interface IKillSwitchService
{
    Task EnableAsync();
    Task DisableAsync();
    bool IsEnabled { get; }
}

public interface IAiAssistant
{
    Task<string> AskAsync(string question, ConnectionStatus? status = null);
    IReadOnlyList<AiMessage> History { get; }
    void ClearHistory();
}

public interface INetworkMonitor
{
    event EventHandler<bool>? ConnectivityChanged;
    void Start();
    void Stop();
}

public interface ILocalizationService
{
    AppLanguage CurrentLanguage { get; }
    string Get(string key);
    string Format(string key, params object[] args);
    void SetLanguage(AppLanguage language);
    event EventHandler? LanguageChanged;
}

public interface IAutoStartService
{
    bool IsEnabled { get; }
    Task SetEnabledAsync(bool enabled);
}

public interface IIpInfoService
{
    Task<(string ip, string country, string countryCode)> GetPublicIpAsync(bool viaProxy, CancellationToken cancellationToken = default);
}

public enum ConflictSeverity
{
    Info,
    Warning,
    Error
}

public sealed class ConflictItem
{
    public string Title { get; init; } = "";
    public string Detail { get; init; } = "";
    public ConflictSeverity Severity { get; init; }
}

public sealed class ConflictReport
{
    public IReadOnlyList<ConflictItem> Items { get; init; } = [];
    public bool HasWarnings => Items.Any(i => i.Severity >= ConflictSeverity.Warning);
}

public interface IConflictDetectionService
{
    Task<ConflictReport> DetectAsync(CancellationToken cancellationToken = default);
}

public interface ILogViewerService
{
    string? GetLatestLogPath();
    Task<string> ReadTailAsync(int maxLines = 400, CancellationToken cancellationToken = default);
}

public interface ISystemProxyManager
{
    Task SetProxyAsync(string host, int port, bool bypassLocal);
    Task SetSocksProxyAsync(string host, int port, bool bypassLocal);
    Task ClearProxyAsync();
    void CleanupStaleProxyOnStartup();
    (bool Enabled, string? Server) GetProxyState();
}
