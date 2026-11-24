using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

public static class SubscriptionItemSettingsHelper
{
    public static SubscriptionItemSettings GetOrCreate(AppSettings settings, string url)
    {
        url = SubscriptionUrlNormalizer.Normalize(url);
        if (!settings.SubscriptionItems.TryGetValue(url, out var item) || item == null)
        {
            item = new SubscriptionItemSettings();
            if (settings.SubscriptionRemarks.TryGetValue(url, out var remark) && !string.IsNullOrWhiteSpace(remark))
            {
                item.Remarks = remark.Trim();
            }

            settings.SubscriptionItems[url] = item;
        }

        return item;
    }

    public static void SyncRemark(AppSettings settings, string url, string remarks)
    {
        url = SubscriptionUrlNormalizer.Normalize(url);
        if (string.IsNullOrWhiteSpace(remarks))
        {
            settings.SubscriptionRemarks.Remove(url);
        }
        else
        {
            settings.SubscriptionRemarks[url] = remarks.Trim();
        }
    }

    public static string? TryGetUserAgent(AppSettings settings, string url)
    {
        url = SubscriptionUrlNormalizer.Normalize(url);
        return settings.SubscriptionItems.TryGetValue(url, out var item) && !string.IsNullOrWhiteSpace(item.UserAgent)
            ? item.UserAgent.Trim()
            : null;
    }

    public static PingType ResolvePingType(AppSettings settings, ServerProfile server)
    {
        var url = SubscriptionUrlNormalizer.Normalize(server.SubscriptionUrl ?? "");
        if (!string.IsNullOrWhiteSpace(url) &&
            settings.SubscriptionItems.TryGetValue(url, out var item) &&
            TryParsePingType(item.PingType, out var perSub))
        {
            return perSub;
        }

        return settings.PingType;
    }

    public static int TryGetRequestTimeoutSeconds(AppSettings settings, string url)
    {
        url = SubscriptionUrlNormalizer.Normalize(url);
        if (settings.SubscriptionItems.TryGetValue(url, out var item) &&
            item.RequestTimeoutSeconds is > 0 and var t)
        {
            return t;
        }

        return 20;
    }

    public static bool TryParsePingType(string? raw, out PingType pingType)
    {
        pingType = PingType.Proxy;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        pingType = raw.Trim().ToLowerInvariant() switch
        {
            "tcp" => PingType.Tcp,
            "icmp" => PingType.Icmp,
            "proxy" => PingType.Proxy,
            "proxy-head" => PingType.ProxyHead,
            _ => PingType.Proxy
        };
        return true;
    }
}