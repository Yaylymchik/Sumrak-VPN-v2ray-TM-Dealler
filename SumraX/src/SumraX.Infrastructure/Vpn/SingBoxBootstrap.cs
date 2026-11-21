using System.Diagnostics;
using SumraX.Core.Interfaces;
using SumraX.Infrastructure.Platform;
using SumraX.Infrastructure.Security;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Ensures sing-box binary is present (TUIC, SSR obfs, Hysteria v1).
/// </summary>
public interface ISingBoxBootstrap
{
    bool IsReady { get; }
    string? StatusMessage { get; }
    Task EnsureAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class SingBoxBootstrap : ISingBoxBootstrap
{
    private const string RepoApi = "https://api.github.com/repos/SagerNet/sing-box/releases/latest";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private readonly ILocalizationService _loc;

    public SingBoxBootstrap(ILocalizationService loc) => _loc = loc;

    static SingBoxBootstrap()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("SumraX-VPN/1.0");
    }

    public bool IsReady => File.Exists(AppPaths.SingBoxPath);
    public string? StatusMessage { get; private set; }

    public async Task EnsureAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();
        if (IsReady)
        {
            StatusMessage = _loc.Get("SingBoxReady");
            return;
        }

        if (OfflineBundle.IsBundled)
        {
            // sing-box optional in offline bundle for proxy-only mode
            StatusMessage = File.Exists(AppPaths.SingBoxPath)
                ? _loc.Get("SingBoxReady")
                : _loc.Get("SingBoxNotFound");
            return;
        }

        await DownloadLatestAsync(progress, cancellationToken);
    }

    private async Task DownloadLatestAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(_loc.Get("SingBoxDownloading"));
        StatusMessage = _loc.Get("SingBoxDownloading");

        var json = await Http.GetStringAsync(RepoApi, cancellationToken);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var assets = doc.RootElement.GetProperty("assets");

        string? downloadUrl = null;
        var pattern = PlatformRuntime.SingBoxAssetPattern;
        foreach (var asset in assets.EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase) &&
                (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                 name.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase)))
            {
                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new InvalidOperationException($"sing-box {pattern} release not found on GitHub.");
        }

        var singBoxDir = Path.GetDirectoryName(AppPaths.SingBoxPath)!;
        var zipPath = Path.Combine(AppPaths.DataRoot, "sing-box-download.zip");

        await using (var stream = await Http.GetStreamAsync(downloadUrl, cancellationToken))
        await using (var file = File.Create(zipPath))
        {
            await stream.CopyToAsync(file, cancellationToken);
        }

        progress?.Report(_loc.Get("SingBoxExtracting"));
        StatusMessage = _loc.Get("SingBoxExtracting");

        if (Directory.Exists(singBoxDir))
        {
            foreach (var f in Directory.GetFiles(singBoxDir))
            {
                File.Delete(f);
            }
        }
        else
        {
            Directory.CreateDirectory(singBoxDir);
        }

        if (downloadUrl.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("tar.gz sing-box packages require manual extraction on this platform.");
        }

        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, singBoxDir, true);
        File.Delete(zipPath);

        FlattenDirectory(singBoxDir);

        if (!IsReady)
        {
            throw new FileNotFoundException($"{PlatformRuntime.SingBoxBinaryName} not found after extraction.", AppPaths.SingBoxPath);
        }

        PlatformRuntime.EnsureExecutable(AppPaths.SingBoxPath);
        TrustedBinaryGuard.EnsureTrustedCore(AppPaths.SingBoxPath);

        StatusMessage = _loc.Get("SingBoxInstalled");
        progress?.Report(StatusMessage);
    }

    private static void FlattenDirectory(string dir)
    {
        foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            var target = Path.Combine(dir, name);
            if (!string.Equals(file, target, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(file, target, true);
            }
        }
    }
}
