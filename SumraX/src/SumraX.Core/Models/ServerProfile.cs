using System.Text.Json.Serialization;
using SumraX.Core.Enums;
using SumraX.Core.Util;

namespace SumraX.Core.Models;

public sealed class ServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayName => ServerDisplayName.FromProfile(Name, Address);
    public string Address { get; set; } = string.Empty;
    public int Port { get; set; }
    public VpnProtocol Protocol { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Encryption { get; set; } = "auto";
    public string Network { get; set; } = "tcp";
    public string Host { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Security { get; set; } = "none";
    public string Sni { get; set; } = string.Empty;
    public string Fingerprint { get; set; } = "chrome";
    public string Flow { get; set; } = string.Empty;
    public string Alpn { get; set; } = string.Empty;
    public string TransportMode { get; set; } = string.Empty;
    public string Extra { get; set; } = string.Empty;
    public string SubscriptionUrl { get; set; } = string.Empty;
    public string CountryCode { get; set; } = "UN";
    public string CountryName { get; set; } = "Unknown";
    public bool IsFavorite { get; set; }
    public int PingMs { get; set; } = -1;
    public double LoadPercent { get; set; }
    public double DownloadMbps { get; set; }
    public double UploadMbps { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public string RawConfig { get; set; } = string.Empty;
}
