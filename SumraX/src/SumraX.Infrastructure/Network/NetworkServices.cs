using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure.Security;
using SumraX.Infrastructure.Vpn;

namespace SumraX.Infrastructure.Network;

public sealed class SpeedTestService : ISpeedTestService
{
    private readonly ISettingsRepository _settings;
    private readonly IVpnEngine _vpn;
    private static readonly ConcurrentDictionary<int, byte> UsedPorts = new();

    public SpeedTestService(ISettingsRepository settings, IVpnEngine vpn)
    {
        _settings = settings;
        _vpn = vpn;
    }

    public async Task<int> MeasurePingAsync(ServerProfile server, CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync();
        return settings.PingType switch
        {
            PingType.Icmp => await MeasureIcmpAsync(server.Address, cancellationToken),
            PingType.Proxy => await MeasureRealPingAsync(server, settings, HttpMethod.Get, cancellationToken),
            PingType.ProxyHead => await MeasureRealPingAsync(server, settings, HttpMethod.Head, cancellationToken),
            _ => await MeasureTcpAsync(server.Address, server.Port, cancellationToken)
        };
    }

    public async Task MeasureAllPingsAsync(IReadOnlyList<ServerProfile> servers, CancellationToken cancellationToken = default)
    {
        if (servers.Count == 0)
        {
            return;
        }

        var settings = await _settings.GetAsync();
        var concurrency = Math.Clamp(settings.RealPingConcurrency <= 0 ? 16 : settings.RealPingConcurrency, 1, 64);
        await Parallel.ForEachAsync(servers, new ParallelOptions
        {
            MaxDegreeOfParallelism = concurrency,
            CancellationToken = cancellationToken
        }, async (server, ct) =>
        {
            server.PingMs = await MeasurePingAsync(server, ct);
        });
    }

    public async Task<int> MeasureProxyPingAsync(CancellationToken cancellationToken = default)
    {
        if (_vpn.State != ConnectionState.Connected)
        {
            return -1;
        }

        var settings = await _settings.GetAsync();
        var method = settings.PingType == PingType.ProxyHead ? HttpMethod.Head : HttpMethod.Get;
        var url = string.IsNullOrWhiteSpace(settings.DelayTestUrl)
            ? "https://www.gstatic.com/generate_204"
            : settings.DelayTestUrl.Trim();

        try
        {
            using var client = await CreateHttpClientAsync(TimeSpan.FromSeconds(6));
            var sw = Stopwatch.StartNew();
            using var response = await client.SendAsync(new HttpRequestMessage(method, url), cancellationToken);
            sw.Stop();
            return (int)response.StatusCode is >= 200 and < 500
                ? (int)sw.ElapsedMilliseconds
                : -1;
        }
        catch
        {
            return -1;
        }
    }

    public async Task<(double download, double upload)> MeasureSpeedAsync(
        IProgress<SpeedTestProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var pingMs = await MeasureProxyPingAsync(cancellationToken);
        progress?.Report(new SpeedTestProgress { Phase = SpeedTestPhase.Ping, PingMs = pingMs });

        using var client = await CreateHttpClientAsync(TimeSpan.FromSeconds(45));
        var download = await MeasureDownloadAsync(client, progress, cancellationToken);
        var upload = await MeasureUploadAsync(client, progress, cancellationToken);
        progress?.Report(new SpeedTestProgress { Phase = SpeedTestPhase.Done, MegabitsPerSecond = upload });
        return (download, upload);
    }

    /// <summary>
    /// Full real ping like v2rayNG: spin temporary xray for this server, HTTP via its local proxy.
    /// </summary>
    private async Task<int> MeasureRealPingAsync(
        ServerProfile server,
        AppSettings settings,
        HttpMethod method,
        CancellationToken cancellationToken)
    {
        // Sing-box-only protocols: full temp-core real ping not wired yet → TCP latency.
        if (CoreSelector.Select(server.Protocol) == VpnCoreKind.SingBox)
        {
            return await MeasureTcpAsync(server.Address, server.Port, cancellationToken);
        }

        if (!File.Exists(AppPaths.XrayPath))
        {
            return -1;
        }

        var socksPort = AllocatePort();
        var httpPort = socksPort + 1;
        UsedPorts[httpPort] = 0;
        var configPath = Path.Combine(Path.GetTempPath(), $"sumrax-ping-{Guid.NewGuid():N}.json");
        Process? process = null;

        try
        {
            var json = XrayConfigBuilder.BuildForSpeedtest(server, settings, socksPort);
            await File.WriteAllTextAsync(configPath, json, cancellationToken);

            TrustedBinaryGuard.EnsureTrustedCore(AppPaths.XrayPath);
            var psi = TrustedBinaryGuard.CreateTrustedStartInfo(
                AppPaths.XrayPath,
                $"run -c \"{configPath}\"",
                Path.GetDirectoryName(AppPaths.XrayPath));
            process = Process.Start(psi);
            if (process == null)
            {
                return -1;
            }

            if (!await WaitForPortAsync(httpPort, 4000, cancellationToken))
            {
                return -1;
            }

            var url = string.IsNullOrWhiteSpace(settings.DelayTestUrl)
                ? "https://www.gstatic.com/generate_204"
                : settings.DelayTestUrl.Trim();

            var handler = new HttpClientHandler
            {
                Proxy = new WebProxy($"http://127.0.0.1:{httpPort}"),
                UseProxy = true
            };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            var sw = Stopwatch.StartNew();
            using var response = await client.SendAsync(new HttpRequestMessage(method, url), cancellationToken);
            sw.Stop();
            return (int)response.StatusCode is >= 200 and < 500
                ? (int)sw.ElapsedMilliseconds
                : -1;
        }
        catch
        {
            return -1;
        }
        finally
        {
            try
            {
                if (process is { HasExited: false })
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }

            process?.Dispose();
            TryDelete(configPath);
            UsedPorts.TryRemove(socksPort, out _);
            UsedPorts.TryRemove(httpPort, out _);
        }
    }

    private static int AllocatePort()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            if (port > 0 && port < 65534 && UsedPorts.TryAdd(port, 0) && UsedPorts.TryAdd(port + 1, 0))
            {
                return port;
            }

            UsedPorts.TryRemove(port, out _);
            UsedPorts.TryRemove(port + 1, out _);
        }

        return 18080 + Random.Shared.Next(1000, 9000);
    }

    private static async Task<bool> WaitForPortAsync(int port, int timeoutMs, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var client = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(200);
                await client.ConnectAsync(IPAddress.Loopback, port, cts.Token);
                return true;
            }
            catch
            {
                await Task.Delay(80, cancellationToken);
            }
        }

        return false;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }

    private static async Task<int> MeasureTcpAsync(string host, int port, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host) || port <= 0)
        {
            return -1;
        }

        try
        {
            var best = -1;
            for (var i = 0; i < 2; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                var sw = Stopwatch.StartNew();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(3000);
                try
                {
                    await socket.ConnectAsync(host, port, cts.Token);
                    sw.Stop();
                    var ms = (int)sw.ElapsedMilliseconds;
                    if (best < 0 || ms < best)
                    {
                        best = ms;
                    }
                }
                catch
                {
                    // next sample
                }
            }

            return best;
        }
        catch
        {
            return -1;
        }
    }

    private static async Task<int> MeasureIcmpAsync(string host, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return -1;
        }

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 3000);
            cancellationToken.ThrowIfCancellationRequested();
            return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
        }
        catch
        {
            return -1;
        }
    }

    private async Task<HttpClient> CreateHttpClientAsync(TimeSpan timeout)
    {
        if (_vpn.State == ConnectionState.Connected && _vpn.IsTunMode)
        {
            return new HttpClient { Timeout = timeout };
        }

        var settings = await _settings.GetAsync();
        var handler = new HttpClientHandler
        {
            Proxy = new WebProxy($"http://127.0.0.1:{settings.LocalHttpPort}"),
            UseProxy = true
        };
        return new HttpClient(handler) { Timeout = timeout };
    }

    private static async Task<double> MeasureDownloadAsync(
        HttpClient client,
        IProgress<SpeedTestProgress>? progress,
        CancellationToken cancellationToken)
    {
        const string url = "https://speed.cloudflare.com/__down?bytes=25000000";
        var sw = Stopwatch.StartNew();
        await using var stream = await client.GetStreamAsync(url, cancellationToken);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            var elapsed = Math.Max(sw.Elapsed.TotalSeconds, 0.001);
            progress?.Report(new SpeedTestProgress
            {
                Phase = SpeedTestPhase.Download,
                MegabitsPerSecond = total * 8.0 / 1_000_000.0 / elapsed
            });
        }

        sw.Stop();
        return total * 8.0 / 1_000_000.0 / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
    }

    private static async Task<double> MeasureUploadAsync(
        HttpClient client,
        IProgress<SpeedTestProgress>? progress,
        CancellationToken cancellationToken)
    {
        const string url = "https://speed.cloudflare.com/__up";
        var payload = new byte[8_000_000];
        Random.Shared.NextBytes(payload);
        using var content = new ByteArrayContent(payload);
        var sw = Stopwatch.StartNew();
        using var response = await client.PostAsync(url, content, cancellationToken);
        sw.Stop();
        _ = response.EnsureSuccessStatusCode();
        var mbps = payload.Length * 8.0 / 1_000_000.0 / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
        progress?.Report(new SpeedTestProgress { Phase = SpeedTestPhase.Upload, MegabitsPerSecond = mbps });
        return mbps;
    }
}

public sealed class IpInfoService : IIpInfoService
{
    private readonly ISettingsRepository _settings;
    private readonly IVpnEngine _vpn;

    public IpInfoService(ISettingsRepository settings, IVpnEngine vpn)
    {
        _settings = settings;
        _vpn = vpn;
    }

    public async Task<(string ip, string country, string countryCode)> GetPublicIpAsync(
        bool viaProxy,
        CancellationToken cancellationToken = default)
    {
        var settings = await _settings.GetAsync();
        var url = string.IsNullOrWhiteSpace(settings.IpApiUrl)
            ? "https://api.ip.sb/geoip"
            : settings.IpApiUrl.Trim();

        try
        {
            HttpClient client;
            if (viaProxy && _vpn.State == ConnectionState.Connected && !_vpn.IsTunMode)
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new WebProxy($"http://127.0.0.1:{settings.LocalHttpPort}"),
                    UseProxy = true
                };
                client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
            }
            else
            {
                client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            }

            using (client)
            {
                var json = await client.GetStringAsync(url, cancellationToken);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var ip = root.TryGetProperty("ip", out var ipEl) ? ipEl.GetString() ?? ""
                    : root.TryGetProperty("query", out var q) ? q.GetString() ?? "" : "";
                var country = root.TryGetProperty("country", out var c) ? c.GetString() ?? ""
                    : root.TryGetProperty("country_name", out var cn) ? cn.GetString() ?? "" : "";
                var code = root.TryGetProperty("country_code", out var cc) ? cc.GetString() ?? ""
                    : root.TryGetProperty("countryCode", out var cc2) ? cc2.GetString() ?? "" : "";
                return (ip, country, code);
            }
        }
        catch
        {
            return ("", "", "");
        }
    }
}

public sealed class NetworkMonitor : INetworkMonitor
{
    private bool _started;

    public event EventHandler<bool>? ConnectivityChanged;

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
        ConnectivityChanged?.Invoke(this, NetworkInterface.GetIsNetworkAvailable());
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
    }

    private void OnAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e) =>
        ConnectivityChanged?.Invoke(this, e.IsAvailable);
}
