using SumraX.Core.Enums;

namespace SumraX.Core.Models;

public sealed class AppSettings
{
    public string Id { get; set; } = "default";
    public bool AutoStart { get; set; }
    public bool KillSwitch { get; set; }
    public bool DnsLeakProtection { get; set; } = true;
    public bool AutoReconnect { get; set; } = true;
    public bool AutoSelectFastestServer { get; set; }
    /// <summary>Smart: ping &amp; pick best on connect. Manual: use selected server.</summary>
    public bool SmartConnectionMode { get; set; } = true;
    public bool AutoRefreshSubscriptions { get; set; } = true;
    public int SubscriptionRefreshIntervalHours { get; set; } = 6;
    /// <summary>parity: minutes between automatic refreshes (re-pulls the feed roughly every 20 minutes).</summary>
    public int SubscriptionRefreshIntervalMinutes { get; set; } = 30;
    public DateTime LastSubscriptionRefreshUtc { get; set; }
    public List<string> SubscriptionUrls { get; set; } = [];
    public AppLanguage Language { get; set; } = AppLanguage.Russian;
    public AppTheme Theme { get; set; } = AppTheme.Cyber;
    public string DnsServer { get; set; } = "1.1.1.1";
    public string RemoteDns { get; set; } = "https://cloudflare-dns.com/dns-query";
    public string? LastSelectedServerId { get; set; }
    public bool OnboardingCompleted { get; set; }
    public string UserDisplayName { get; set; } = "User";
    public string RegionCode { get; set; } = "RU";
    public string TimeZoneId { get; set; } = "Russian Standard Time";
    public string? ActiveServerId { get; set; }
    public int LocalSocksPort { get; set; } = 10808;
    public int LocalHttpPort { get; set; } = 10809;
    public bool UpdateSubscriptionsOnStart { get; set; } = true;
    public bool ConnectOnStart { get; set; }
    public bool PingOnStart { get; set; } = true;
    public PingType PingType { get; set; } = PingType.Proxy;
    public bool AllowLanConnections { get; set; }
    public bool BypassLocalForSystemProxy { get; set; } = true;
    public bool UseSystemProxy { get; set; } = true;
    public bool UseLocalDns { get; set; }
    public bool UseDnsFromJson { get; set; }
    public bool EnableServerResolving { get; set; }
    public bool IgnoreDuplicateServers { get; set; } = true;
    /// <summary>After subscription refresh, remove profiles that disappeared from the feed (and reclaim untagged duplicates).</summary>
    public bool PurgeOldProfilesOnSubscriptionUpdate { get; set; } = true;
    public bool MinimizeToTray { get; set; } = true;
    public bool UseTunMode { get; set; } = true;
    public TunProvider TunProvider { get; set; } = TunProvider.Tun2Proxy;
    public int TunMtu { get; set; } = 1500;
    public TunStackMode TunStackMode { get; set; } = TunStackMode.Mixed;
    public bool TunDnsEnable { get; set; } = true;
    public bool SniffTraffic { get; set; }
    public bool RouteOnly { get; set; }
    public bool Ipv6Enabled { get; set; }
    public bool PreferIpv6 { get; set; }
    public bool FakeDnsEnabled { get; set; }
    public string DomesticDns { get; set; } = "8.8.8.8";
    public string DnsHosts { get; set; } = "";
    public string CoreLogLevel { get; set; } = "warning";
    public bool SocksEnableUdp { get; set; } = true;
    public bool MuxEnabled { get; set; }
    public int MuxConcurrency { get; set; } = 8;
    public int MuxXudpConcurrency { get; set; } = 8;
    public string MuxXudpQuic { get; set; } = "reject";
    public bool FragmentEnabled { get; set; }
    public string FragmentPackets { get; set; } = "tlshello";
    public string FragmentLength { get; set; } = "50-100";
    public string FragmentInterval { get; set; } = "10-20";
    /// <summary>Xray freedom.fragment.maxSplit range, e.g. 10-20.</summary>
    public string FragmentMaxSplit { get; set; } = "10-20";
    /// <summary>Xray | Advanced — Advanced allows free-form packet ranges like Sumrak.</summary>
    public string FragmentEngine { get; set; } = "Xray";
    public bool NoiseEnabled { get; set; }
    public string NoiseRand { get; set; } = "10-20";
    public string NoiseDelay { get; set; } = "10-16";
    public bool AutoSortAfterTest { get; set; } = true;
    public bool AutoRemoveInvalidAfterTest { get; set; }
    public string DelayTestUrl { get; set; } = "https://www.gstatic.com/generate_204";
    public string IpApiUrl { get; set; } = "https://api.ip.sb/geoip";
    public int RealPingConcurrency { get; set; } = 16;
    public bool SpeedDisplayEnabled { get; set; } = true;
    public bool ConfirmRemove { get; set; }
    public bool RoutingEnabled { get; set; }
    public ClientUserAgentPreset SubscriptionUserAgent { get; set; } = ClientUserAgentPreset.ChromeWindows;
    public RoutingMode RoutingMode { get; set; } = RoutingMode.BypassPrivate;
    public List<string> SplitTunnelProcesses { get; set; } = [];
    public string DomainDirectRules { get; set; } = "";
    public bool CheckConflictsOnStart { get; set; } = true;
    public DateTime LastBackupUtc { get; set; }
    public Dictionary<string, SubscriptionTrafficInfo> SubscriptionTraffic { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SubscriptionRemarks { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> PinnedSubscriptionUrls { get; set; } = [];
    public bool SendHwidHeader { get; set; } = true;
    public string? CustomHwidValue { get; set; }
}

public sealed class UserProfile
{
    public string DisplayName { get; set; } = "User";
    public string Email { get; set; } = string.Empty;
    public SubscriptionPlan Plan { get; set; } = SubscriptionPlan.Free;
    public DateTime? SubscriptionExpiresUtc { get; set; }
    public bool IsActive => Plan != SubscriptionPlan.Free &&
        (SubscriptionExpiresUtc == null || SubscriptionExpiresUtc > DateTime.UtcNow);
}

public sealed class ConnectionStatus
{
    public ConnectionState State { get; set; } = ConnectionState.Disconnected;
    public string? PublicIp { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public int PingMs { get; set; } = -1;
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public TimeSpan ConnectedDuration { get; set; }
    public string? ServerName { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class AiMessage
{
    public bool IsUser { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
