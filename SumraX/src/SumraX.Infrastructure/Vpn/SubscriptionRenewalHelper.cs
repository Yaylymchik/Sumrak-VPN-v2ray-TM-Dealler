using SumraX.Core.Interfaces;
using SumraX.Core.Models;

namespace SumraX.Infrastructure.Vpn;

/// <summary>Android <c>SubscriptionRenewalReminder</c> parity for desktop home UI.</summary>
public static class SubscriptionRenewalHelper
{
    private const int LowDaysThreshold = 7;

    public static int? RemainingDays(SubscriptionTrafficInfo? traffic)
    {
        if (traffic?.ExpireUtc is not { } exp)
        {
            return null;
        }

        var days = (int)Math.Ceiling((exp - DateTime.UtcNow).TotalDays);
        return Math.Max(0, days);
    }

    public static string? HomeBannerRu(int? days, bool isRu = true)
    {
        if (days == null)
        {
            return null;
        }

        return days switch
        {
            0 => isRu ? "Продли подписку" : "Renew subscription",
            <= LowDaysThreshold => isRu
                ? $"Осталось {days} дн. — продлите подписку"
                : $"{days} day(s) left — renew subscription",
            _ => null
        };
    }

}