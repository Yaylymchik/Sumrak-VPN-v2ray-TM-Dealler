using System.Diagnostics;
using SumraX.Core.Interfaces;
using SumraX.Infrastructure.Platform;
using SumraX.Infrastructure.Security;

namespace SumraX.Infrastructure.Vpn;

/// <summary>
/// Ensures Xray-core binary is present; downloads from GitHub if missing.
/// </summary>
public interface IXrayBootstrap
{
    bool IsReady { get; }
    string? StatusMessage { get; }
    Task EnsureAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public sealed class XrayBootstrap : IXrayBootstrap
{
    private const string RepoApi = "https://api.github.com/repos/XTLS/Xray-core/releases/latest";
    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };
    private readonly ILocalizationService _loc;

    public XrayBootstrap(ILocalizationService loc) => _loc = loc;

    static XrayBootstrap()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("SumraX-VPN/1.0");
    }

    public bool IsReady => File.Exists(AppPaths.XrayPath);
    public string? StatusMessage { get; private set; }

    public async Task EnsureAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        AppPaths.EnsureDirectories();
        if (IsReady)
        {
            StatusMessage = _loc.Get("BootstrapReady");
            return;
        }

        if (OfflineBundle.IsBundled)
        {
            OfflineBundle.EnsureCorePresent(AppPaths.XrayPath, "Xray-core");
            StatusMessage = _loc.Get("BootstrapReady");
            return;
        }

        await DownloadLatestXrayAsync(progress, cancellationToken);
    }

    private async Task DownloadLatestXrayAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report(_loc.Get("BootstrapDownloading"));
        StatusMessage = _loc.Get("BootstrapDownloading");

        var json = await Http.GetStringAsync(RepoApi, cancellationToken);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var assets = doc.RootElement.GetProperty("assets");

        string? downloadUrl = null;
        var pattern = PlatformRuntime.XrayAssetPattern;
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
            throw new InvalidOperationException($"Xray {pattern} release not found on GitHub.");
        }

        var xrayDir = Path.GetDirectoryName(AppPaths.XrayPath)!;
        var zipPath = Path.Combine(AppPaths.DataRoot, "xray-download.zip");

        await using (var stream = await Http.GetStreamAsync(downloadUrl, cancellationToken))
        await using (var file = File.Create(zipPath))
        {
            await stream.CopyToAsync(file, cancellationToken);
        }

        progress?.Report(_loc.Get("BootstrapExtracting"));
        StatusMessage = _loc.Get("BootstrapExtracting");

        if (Directory.Exists(xrayDir))
        {
            foreach (var f in Directory.GetFiles(xrayDir))
            {
                File.Delete(f);
            }
        }
        else
        {
            Directory.CreateDirectory(xrayDir);
        }

        if (downloadUrl.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
        {
            throw new NotSupportedException("tar.gz Xray packages require manual extraction on this platform.");
        }

        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, xrayDir, true);
        File.Delete(zipPath);

        FlattenXrayDirectory(xrayDir);

        if (!IsReady)
        {
            throw new FileNotFoundException($"{PlatformRuntime.XrayBinaryName} not found after extraction.", AppPaths.XrayPath);
        }

        PlatformRuntime.EnsureExecutable(AppPaths.XrayPath);
        TrustedBinaryGuard.EnsureTrustedCore(AppPaths.XrayPath);

        StatusMessage = _loc.Get("BootstrapInstalled");
        progress?.Report(StatusMessage);
    }

    private static void FlattenXrayDirectory(string xrayDir)
    {
        foreach (var file in Directory.GetFiles(xrayDir, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetFileName(file);
            var target = Path.Combine(xrayDir, name);
            if (!string.Equals(file, target, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(file, target, true);
            }
        }
    }
}
