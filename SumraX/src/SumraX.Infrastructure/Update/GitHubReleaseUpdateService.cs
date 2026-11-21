using System.Net.Http.Json;
using System.Reflection;
using NLog;

namespace SumraX.Infrastructure.Update;

public sealed class GitHubReleaseUpdateService
{
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();
    private const string ReleasesApi =
        "https://api.github.com/repos/Yaylymchik/Sumrak-VPN-v2ray-PC/releases/latest";

    private readonly HttpClient _http;

    public GitHubReleaseUpdateService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient();
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _http.DefaultRequestHeaders.Add("User-Agent", "Sumrak-VPN-Desktop/2.2.21");
        }
    }

    public async Task<ReleaseCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var current = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.0.0";
        try
        {
            using var response = await _http.GetAsync(ReleasesApi, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new ReleaseCheckResult(current, null, false, "http");
            }

            var doc = await response.Content.ReadFromJsonAsync<GhRelease>(cancellationToken: cancellationToken);
            var latest = doc?.TagName?.TrimStart('v', 'V') ?? "";
            var newer = CompareSemver(latest, current) > 0;
            return new ReleaseCheckResult(current, latest, newer, doc?.HtmlUrl ?? ReleasesApi);
        }
        catch (Exception ex)
        {
            Log.Warn(ex, "Release check failed");
            return new ReleaseCheckResult(current, null, false, ex.Message);
        }
    }

    private static int CompareSemver(string a, string b)
    {
        static int[] P(string v) => v.Split('.').Select(s => int.TryParse(s, out var n) ? n : 0).Concat([0, 0, 0]).Take(3).ToArray();
        var av = P(a);
        var bv = P(b);
        for (var i = 0; i < 3; i++)
        {
            var c = av[i].CompareTo(bv[i]);
            if (c != 0)
            {
                return c;
            }
        }

        return 0;
    }

    private sealed class GhRelease
    {
        public string? TagName { get; set; }
        public string? HtmlUrl { get; set; }
    }
}

public sealed record ReleaseCheckResult(
    string CurrentVersion,
    string? LatestVersion,
    bool UpdateAvailable,
    string DetailUrl);