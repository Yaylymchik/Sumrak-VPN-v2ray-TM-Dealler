using SumraX.Core.Models;

namespace SumraX.Core.Vpn;

/// <summary>
/// v2rayNG style "smart" profile selection.
///
/// Pure ping ordering (what the app used before) picks a node that answers a delay probe but is
/// already saturated, so the session feels slow. ranks candidates by a weighted score that
/// mixes measured delay, reported load and recent usage, and it prefers a node that was proven to
/// work before. This type keeps that policy in one place so the connect path, the server list and
/// the picker overlay all agree on what "best" means.
///
/// The score is a cost: lower is better. A profile that failed its probe gets a score of
/// <see cref="UnreachableScore"/> so it can only be chosen when nothing else is available.
/// </summary>
public static class SmartProfileRanker
{
    public const double UnreachableScore = 1_000_000d;

    /// <summary>Nodes that answered slower than this are treated as alive but poor.</summary>
    private const int SlowPingThresholdMs = 800;

    /// <summary>Load above this is penalised harder (queueing delay grows non-linearly).</summary>
    private const double HighLoadThreshold = 70d;

    /// <summary>
    /// Cost of a profile. Lower is better.
    /// </summary>
    /// <param name="profile">Candidate profile.</param>
    /// <param name="preferFavorite">Add a discount for favourites (user intent beats raw speed).</param>
    public static double Score(ServerProfile profile, bool preferFavorite = true)
    {
        if (profile.PingMs <= 0)
        {
            // Header/base64 feeds report 0 for "not probed yet"; only a negative value means failed.
            return profile.PingMs < 0 ? UnreachableScore : UnreachableScore / 2;
        }

        var score = (double)profile.PingMs;

        // Load: reported percentage. Above the knee the penalty grows quadratically, because a node
        // at 90% load adds far more jitter than a linear model suggests.
        if (profile.LoadPercent > 0)
        {
            score += profile.LoadPercent * 1.5d;
            if (profile.LoadPercent > HighLoadThreshold)
            {
                var over = profile.LoadPercent - HighLoadThreshold;
                score += over * over * 0.5d;
            }
        }

        // A node that already answered but is very slow should lose to a fast, unproven node only
        // if that unproven node is not scored at all, so keep the slow-node penalty mild.
        if (profile.PingMs > SlowPingThresholdMs)
        {
            score += 100d;
        }

        // Throughput history from a previous speed test: reward measured bandwidth.
        if (profile.DownloadMbps > 0)
        {
            score -= Math.Min(150d, profile.DownloadMbps * 3d);
        }

        // Recency: a node used successfully before is a safer default than a cold one.
        if (profile.LastUsedAt is { } used)
        {
            var age = DateTime.UtcNow - used;
            if (age < TimeSpan.FromHours(1))
            {
                score -= 40d;
            }
            else if (age < TimeSpan.FromDays(1))
            {
                score -= 20d;
            }
        }

        if (preferFavorite && profile.IsFavorite)
        {
            score -= 60d;
        }

        return Math.Max(1d, score);
    }

    /// <summary>
    /// Best reachable profile, or null when every candidate failed its probe.
    /// </summary>
    public static ServerProfile? PickBest(IEnumerable<ServerProfile> profiles, bool preferFavorite = true)
    {
        ServerProfile? best = null;
        var bestScore = double.MaxValue;
        foreach (var profile in profiles)
        {
            var score = Score(profile, preferFavorite);
            if (score >= UnreachableScore)
            {
                continue;
            }

            if (score < bestScore)
            {
                bestScore = score;
                best = profile;
            }
        }

        return best;
    }

    /// <summary>Ordering used by the server list: smart score, then name for stability.</summary>
    public static IOrderedEnumerable<ServerProfile> OrderBySmart(
        IEnumerable<ServerProfile> profiles,
        bool preferFavorite = true) =>
        profiles
            .OrderBy(p => Score(p, preferFavorite))
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
}
