using System.Net;
using System.Net.Http;
using System.Text.Json;
using SumraX.Core;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Security;

namespace SumraX.Infrastructure.Vpn;

public sealed class ConfigImportService : IConfigImportService
{
    private static readonly string[] SubscriptionUserAgents =
    [
        "v2rayN/7.0",
        "v2rayN/6.0",
        "SumraX-VPN/1.0",
        "clash-verge/v2.0.0",
        "HiddifyNext/1.0",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36"
    ];

    private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();

    private readonly ILocalizationService _loc;
    private readonly ISettingsRepository _settings;
    private readonly IVpnEngine _vpn;

    public ConfigImportService(ILocalizationService loc, ISettingsRepository settings, IVpnEngine vpn)
    {
        _loc = loc;
        _settings = settings;
        _vpn = vpn;
    }

    public async Task<SubscriptionImportResult> ImportFromSubscriptionUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        url = SubscriptionUrlNormalizer.Normalize(url);
        if (string.IsNullOrWhiteSpace(url))
        {
            return new SubscriptionImportResult { Error = _loc.Get("SubInvalidUrl") };
        }

        if (CryptLinkDecryptor.IsCryptLink(url))
        {
            if (!CryptLinkDecryptor.TryDecrypt(url, out var decrypted))
            {
                return new SubscriptionImportResult { Error = _loc.Get("CryptLinkDecryptFailed") };
            }

            return await ImportFromSubscriptionUrlAsync(decrypted, cancellationToken);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            return new SubscriptionImportResult { Error = _loc.Get("SubInvalidUrl") };
        }

        Exception? lastError = null;
        var attempts = 0;
        var settings = await _settings.GetAsync();
        var perSubUa = SubscriptionItemSettingsHelper.TryGetUserAgent(settings, url);
        var userAgents = await BuildSubscriptionUserAgentsAsync(cancellationToken, perSubUa);
        var routes = new List<WebProxy?>(BuildFetchProxyCandidates(settings.LocalHttpPort)) { null };
        Log.Info($"Subscription fetch start: {DescribeUrl(url)}; routes=[{string.Join(" | ", routes.Select(DescribeProxy))}]");

        foreach (var proxy in routes)
        {
            foreach (var userAgent in userAgents)
            {
                attempts++;
                try
                {
                    var timeout = SubscriptionItemSettingsHelper.TryGetRequestTimeoutSeconds(settings, url);
                    using var client = await CreateHttpClientAsync(userAgent, proxy, url, timeout, cancellationToken);
                    using var response = await client.GetAsync(uri, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        continue;
                    }
    
                    SubscriptionTrafficInfo? traffic = null;
                    if (response.Headers.TryGetValues("subscription-userinfo", out var userInfoValues))
                    {
                        traffic = SubscriptionUserInfoParser.Parse(userInfoValues.FirstOrDefault(), url);
                    }

                    string? profileTitle = null;
                    if (response.Headers.TryGetValues("profile-title", out var titleValues))
                    {
                        profileTitle = titleValues.FirstOrDefault()?.Trim();
                    }
                    else if (response.Headers.TryGetValues("Profile-Title", out var titleValues2))
                    {
                        profileTitle = titleValues2.FirstOrDefault()?.Trim();
                    }
    
                    var result = await ImportFromSubscriptionTextAsync(content);
                    Log.Debug($"Subscription fetch via UA '{userAgent}': {content.Length} chars, servers={result.Servers.Count}, error={result.Error ?? "none"}");
                    if (result.Success)
                    {
                        return new SubscriptionImportResult
                        {
                            Servers = result.Servers,
                            Skipped = result.Skipped,
                            Traffic = traffic,
                            ProfileTitle = profileTitle
                        };
                    }
    
                    lastError = new InvalidOperationException(result.Error);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Subscription fetch via UA '{userAgent}' over {DescribeProxy(proxy)} failed: {ex.GetType().Name}: {ex.Message}");
                    lastError = ex;
                    if (ex is HttpRequestException or System.Net.Sockets.SocketException or TaskCanceledException or TimeoutException)
                    {
                        // Network-level failure: retrying other user agents cannot help, bail out early
                        // and let the next route (tunnel inbound -> system proxy/direct) be tried.
                        break;
                    }
                }
            }
        }

        Log.Warn($"Subscription fetch failed on all routes after {attempts} attempt(s): {lastError?.GetType().Name}: {lastError?.Message}");
        return new SubscriptionImportResult
        {
            Error = _loc.Format("SubFetchFailed", lastError?.Message ?? "unknown")
        };
    }

    public Task<SubscriptionImportResult> ImportFromSubscriptionTextAsync(string text)
    {
        text = text.Trim().Trim('\uFEFF');
        if (string.IsNullOrWhiteSpace(text))
        {
            return Task.FromResult(new SubscriptionImportResult { Error = _loc.Get("SubEmpty") });
        }

        var servers = new List<ServerProfile>();
        var skipped = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in V2rayNSubscriptionParser.Parse(text))
        {
            if (string.IsNullOrWhiteSpace(profile.Address))
            {
                skipped++;
                continue;
            }

            var key = $"{profile.Protocol}|{profile.Address}|{profile.Port}|{profile.UserId}|{profile.Name}";
            if (!seen.Add(key))
            {
                skipped++;
                continue;
            }

            servers.Add(profile);
        }

        // Clash YAML (non-v2rayN but common alongside v2rayN subs)
        if (servers.Count == 0 && ClashProxyParser.LooksLikeClashYaml(text))
        {
            foreach (var profile in ClashProxyParser.ParseProxies(text))
            {
                if (string.IsNullOrWhiteSpace(profile.Address))
                {
                    skipped++;
                    continue;
                }

                var key = $"{profile.Protocol}|{profile.Address}|{profile.Port}|{profile.UserId}|{profile.Name}";
                if (!seen.Add(key))
                {
                    skipped++;
                    continue;
                }

                servers.Add(profile);
            }
        }

        // Legacy path: nested decode + link scan (fallback)
        if (servers.Count == 0)
        {
            var content = SubscriptionContentDecoder.TryDecodeNestedBase64(text);
            foreach (var link in SubscriptionContentDecoder.ExtractLinks(content))
            {
                var profile = ProxyLinkParser.TryParse(link);
                if (profile == null || string.IsNullOrWhiteSpace(profile.Address))
                {
                    skipped++;
                    continue;
                }

                var key = $"{profile.Protocol}|{profile.Address}|{profile.Port}|{profile.UserId}|{profile.Name}";
                if (!seen.Add(key))
                {
                    skipped++;
                    continue;
                }

                profile.RawConfig = link;
                servers.Add(profile);
            }

            if (content.StartsWith('{') || content.StartsWith('['))
            {
                foreach (var profile in ExtractProfilesFromJson(content))
                {
                    var key = $"{profile.Protocol}|{profile.Address}|{profile.Port}|{profile.UserId}|{profile.Name}";
                    if (seen.Add(key))
                    {
                        profile.RawConfig = content;
                        servers.Add(profile);
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }
        }

        if (servers.Count == 0)
        {
            return Task.FromResult(new SubscriptionImportResult
            {
                Skipped = skipped,
                Error = _loc.Get("SubNoServers")
            });
        }

        return Task.FromResult(new SubscriptionImportResult { Servers = servers, Skipped = skipped });
    }

    public Task<ServerProfile?> ImportFromLinkAsync(string link) =>
        Task.FromResult(ProxyLinkParser.TryParse(link));

    public Task<ServerProfile?> ImportFromJsonAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("outbounds", out var outbounds) && outbounds.ValueKind == JsonValueKind.Array)
            {
                foreach (var outbound in outbounds.EnumerateArray())
                {
                    var profile = SingBoxConfigParser.TryParseOutbound(outbound);
                    if (profile != null)
                    {
                        profile.RawConfig = json;
                        return Task.FromResult<ServerProfile?>(profile);
                    }
                }
            }

            if (root.TryGetProperty("add", out _))
            {
                var vmess = ProxyLinkParser.ParseVmessJson(root);
                vmess.RawConfig = json;
                return Task.FromResult<ServerProfile?>(vmess);
            }

            var parsed = SingBoxConfigParser.TryParseOutbound(root);
            if (parsed != null)
            {
                parsed.RawConfig = json;
            }

            return Task.FromResult(parsed);
        }
        catch
        {
            return Task.FromResult<ServerProfile?>(null);
        }
    }

    public async Task<ServerProfile?> ImportFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(filePath);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        if (ext is ".yaml" or ".yml" || ClashProxyParser.LooksLikeClashYaml(content))
        {
            var first = ClashProxyParser.ParseProxies(content).FirstOrDefault();
            if (first != null)
            {
                return first;
            }
        }

        if (content.Contains("://"))
        {
            var result = await ImportFromSubscriptionTextAsync(content);
            return result.Success ? result.Servers.FirstOrDefault() : null;
        }

        return await ImportFromJsonAsync(content);
    }

    private static IEnumerable<ServerProfile> ExtractProfilesFromJson(string json)
    {
        var profiles = new List<ServerProfile>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            ExtractFromJsonElement(doc.RootElement, profiles);
        }
        catch
        {
            // not json
        }

        return profiles;
    }

    private static void ExtractFromJsonElement(JsonElement root, List<ServerProfile> profiles)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        var profile = SingBoxConfigParser.TryParseOutbound(item) ??
                                      ClashProxyParser.TryParseProxyObject(item);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                }
                break;

            case JsonValueKind.Object:
                if (root.TryGetProperty("outbounds", out var outbounds))
                {
                    foreach (var outbound in outbounds.EnumerateArray())
                    {
                        var profile = SingBoxConfigParser.TryParseOutbound(outbound);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                }

                if (root.TryGetProperty("proxies", out var proxies))
                {
                    foreach (var proxy in proxies.EnumerateArray())
                    {
                        var profile = ClashProxyParser.TryParseProxyObject(proxy);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                        }
                    }
                }

                if (root.TryGetProperty("add", out _))
                {
                    profiles.Add(ProxyLinkParser.ParseVmessJson(root));
                }

                break;
        }
    }

    private async Task<IReadOnlyList<string>> BuildSubscriptionUserAgentsAsync(
        CancellationToken cancellationToken,
        string? subscriptionCustomUserAgent = null)
    {
        _ = cancellationToken;
        var settings = await _settings.GetAsync();
        var list = new List<string>();
        if (!string.IsNullOrWhiteSpace(subscriptionCustomUserAgent))
        {
            list.Add(subscriptionCustomUserAgent.Trim());
        }

        var preferred = ClientUserAgentProfiles.GetHttpUserAgent(settings.SubscriptionUserAgent);
        if (!list.Contains(preferred, StringComparer.Ordinal))
        {
            list.Add(preferred);
        }
        foreach (var fallback in SubscriptionUserAgents)
        {
            if (!list.Contains(fallback, StringComparer.Ordinal))
            {
                list.Add(fallback);
            }
        }

        return list;
    }

    private static readonly int[] KnownLocalProxyPorts = [10809, 2080, 7890, 8080, 8889, 3128, 1080, 10808];

    /// <summary>
    /// always has its core up, so the subscription request travels through the local inbound and
    /// reaches blocked hosts. SumraX may be disconnected (or in TUN mode, where Windows has no system
    /// proxy at all), so we probe every local inbound that actually accepts a TCP connection and use
    /// it as an HTTP proxy. Only listening ports become routes, so a closed port costs ~200 ms instead
    /// of a 60 s timeout. The final route is the default handler (system proxy / direct).
    /// </summary>
    private static IReadOnlyList<WebProxy> BuildFetchProxyCandidates(int primaryPort)
    {
        var candidates = new List<WebProxy>();
        var seen = new HashSet<int>();

        void Consider(int port)
        {
            if (port is <= 0 or > 65535 || !seen.Add(port))
            {
                return;
            }

            if (!IsLocalPortOpen(port))
            {
                return;
            }

            candidates.Add(new WebProxy(new Uri($"http://127.0.0.1:{port}")) { BypassProxyOnLocal = false });
            Log.Debug($"Subscription route candidate: http://127.0.0.1:{port}");
        }

        Consider(primaryPort);
        foreach (var port in KnownLocalProxyPorts)
        {
            Consider(port);
        }

        return candidates;
    }

    private static bool IsLocalPortOpen(int port)
    {
        try
        {
            using var probe = new System.Net.Sockets.TcpClient();
            var connect = probe.ConnectAsync("127.0.0.1", port);
            if (!connect.Wait(TimeSpan.FromMilliseconds(250)))
            {
                return false;
            }

            return probe.Connected;
        }
        catch
        {
            return false;
        }
    }

    private static string DescribeProxy(WebProxy? proxy) =>
        proxy == null ? "system proxy/direct" : $"local inbound {proxy.Address}";

    private static string DescribeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return "<invalid url>";
        }

        var tail = uri.Fragment.Length > 1 ? uri.Fragment[..Math.Min(uri.Fragment.Length, 9)] : string.Empty;
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}{tail}";
    }

    private async Task<HttpClient> CreateHttpClientAsync(
        string userAgent,
        WebProxy? proxy,
        string subscriptionUrl,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        if (proxy != null)
        {
            handler.UseProxy = true;
            handler.Proxy = proxy;
        }
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120)) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
        foreach (var pair in await HwidManager.SubscriptionHeadersAsync(_settings, subscriptionUrl).WaitAsync(cancellationToken))
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation(pair.Key, pair.Value);
        }

        return client;
    }
}
