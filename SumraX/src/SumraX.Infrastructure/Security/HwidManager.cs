using System.Security.Cryptography;
using System.Text;
using SumraX.Core.Interfaces;
using SumraX.Infrastructure.Vpn;

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

    public static async Task<IReadOnlyDictionary<string, string>> SubscriptionHeadersAsync(
        ISettingsRepository settings,
        string? subscriptionUrl = null)
    {
        var s = await settings.GetAsync();
        var enabled = s.SendHwidHeader;
        if (!string.IsNullOrWhiteSpace(subscriptionUrl))
        {
            var item = SubscriptionItemSettingsHelper.GetOrCreate(s, subscriptionUrl);
            if (item.SendHwidHeader == false)
            {
                return new Dictionary<string, string>();
            }

            if (item.SendHwidHeader == true)
            {
                enabled = true;
            }
        }
        else if (!enabled)
        {
            return new Dictionary<string, string>();
        }

        string hwid;
        if (!string.IsNullOrWhiteSpace(subscriptionUrl))
        {
            var item = SubscriptionItemSettingsHelper.GetOrCreate(s, subscriptionUrl);
            if (item.UseAlternativeHwid == true && !string.IsNullOrWhiteSpace(item.AlternativeHwidValue))
            {
                hwid = item.AlternativeHwidValue.Trim();
            }
            else if (!string.IsNullOrWhiteSpace(s.CustomHwidValue))
            {
                hwid = s.CustomHwidValue.Trim();
            }
            else
            {
                hwid = ResolveMachineHwid();
            }
        }
        else
        {
            hwid = await ResolveHwidAsync(settings);
        }

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