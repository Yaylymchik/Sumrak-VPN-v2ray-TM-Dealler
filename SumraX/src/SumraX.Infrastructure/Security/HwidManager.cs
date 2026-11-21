using System.Security.Cryptography;
using System.Text;
using SumraX.Core.Interfaces;

namespace SumraX.Infrastructure.Security;

/// <summary>Sumrak X-hwid for subscription HTTP requests.</summary>
public static class HwidManager
{
    public static async Task<bool> IsSendEnabledAsync(ISettingsRepository settings)
    {
        var s = await settings.GetAsync();
        return s.SendHwidHeader;
    }

    public static async Task<string> ResolveHwidAsync(ISettingsRepository settings)
    {
        var s = await settings.GetAsync();
        var custom = s.CustomHwidValue?.Trim();
        if (!string.IsNullOrEmpty(custom))
        {
            return custom;
        }

        return ResolveMachineHwid();
    }

    public static string ResolveMachineHwid()
    {
        var id = $"{Environment.MachineName}|{Environment.UserName}|{Environment.OSVersion.Platform}";
        if (string.IsNullOrWhiteSpace(id))
        {
            return "";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(id.Trim()));
        return Convert.ToHexString(hash).ToLowerInvariant()[..32];
    }

    public static async Task<IReadOnlyDictionary<string, string>> SubscriptionHeadersAsync(ISettingsRepository settings)
    {
        if (!await IsSendEnabledAsync(settings))
        {
            return new Dictionary<string, string>();
        }

        var hwid = await ResolveHwidAsync(settings);
        if (string.IsNullOrWhiteSpace(hwid))
        {
            return new Dictionary<string, string>();
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-hwid"] = hwid,
            ["X-HWID"] = hwid,
        };
    }
}