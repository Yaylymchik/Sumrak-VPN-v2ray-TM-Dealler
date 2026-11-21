using System.Text.Json;

namespace SumraX.Infrastructure.Security;

public sealed class BinaryIntegrityService
{
    private readonly Dictionary<string, string> _hashes = new(StringComparer.OrdinalIgnoreCase);

    public bool LoadManifest()
    {
        _hashes.Clear();
        var manifestPath = Path.Combine(AppPaths.InstallRoot, "bin", "integrity.json");
        if (!File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            if (!doc.RootElement.TryGetProperty("files", out var files))
            {
                return false;
            }

            foreach (var entry in files.EnumerateObject())
            {
                var hash = entry.Value.GetString();
                if (!string.IsNullOrWhiteSpace(hash))
                {
                    _hashes[entry.Name] = hash;
                }
            }

            return _hashes.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    public bool VerifyCore(string relativePath, string fullPath)
    {
        if (!_hashes.TryGetValue(relativePath.Replace('\\', '/'), out var expected))
        {
            return true;
        }

        if (!File.Exists(fullPath))
        {
            return false;
        }

        var actual = TrustedBinaryGuard.ComputeSha256(fullPath);
        return actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    public void VerifyBundledCoresOrThrow()
    {
        if (_hashes.Count == 0)
        {
            return;
        }

        var checks = new (string Relative, string Full)[]
        {
            ($"bin/xray/{Platform.PlatformRuntime.XrayBinaryName}", AppPaths.XrayPath),
            ($"bin/sing-box/{Platform.PlatformRuntime.SingBoxBinaryName}", AppPaths.SingBoxPath)
        };

        foreach (var (relative, full) in checks)
        {
            if (!File.Exists(full))
            {
                continue;
            }

            if (!VerifyCore(relative, full))
            {
                throw new InvalidOperationException($"Integrity check failed for {relative}. Reinstall Sumrak VPN.");
            }
        }
    }
}
