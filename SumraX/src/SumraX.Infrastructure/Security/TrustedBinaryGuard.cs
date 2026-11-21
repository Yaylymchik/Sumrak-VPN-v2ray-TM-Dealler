using System.Diagnostics;
using System.Security.Cryptography;
using SumraX.Infrastructure.Platform;

namespace SumraX.Infrastructure.Security;

/// <summary>
/// Validates that only trusted binaries from the install tree are launched.
/// </summary>
public static class TrustedBinaryGuard
{
    private const long MinCoreBytes = 256 * 1024;

    public static void EnsureTrustedCore(string corePath)
    {
        if (string.IsNullOrWhiteSpace(corePath) || !File.Exists(corePath))
        {
            throw new FileNotFoundException("VPN core binary not found.", corePath);
        }

        var fullPath = Path.GetFullPath(corePath);
        if (!IsUnderInstallRoot(fullPath))
        {
            throw new InvalidOperationException($"Untrusted core path: {fullPath}");
        }

        var name = Path.GetFileName(fullPath);
        if (!name.Equals(PlatformRuntime.XrayBinaryName, StringComparison.OrdinalIgnoreCase) &&
            !name.Equals(PlatformRuntime.SingBoxBinaryName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unexpected core binary: {name}");
        }

        var size = new FileInfo(fullPath).Length;
        if (size < MinCoreBytes)
        {
            throw new InvalidOperationException($"Core binary appears invalid (size {size} bytes).");
        }
    }

    public static bool IsUnderInstallRoot(string fullPath)
    {
        var root = Path.GetFullPath(AppPaths.InstallRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var path = Path.GetFullPath(fullPath);
        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
               path.Equals(root, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetSystem32Binary(string exeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("GetSystem32Binary is Windows-only.");
        }

        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), exeName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"System binary not found: {exeName}", path);
        }

        return path;
    }

    public static string GetWindowsBinary(string relativePath)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), relativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Windows binary not found: {relativePath}", path);
        }

        return path;
    }

    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    public static bool TryValidateLocalPath(string path, out string fullPath)
    {
        fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            _ = Path.GetFullPath(fullPath);
            return !fullPath.Contains("..", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static ProcessStartInfo CreateTrustedStartInfo(string fileName, string arguments, string? workingDirectory = null)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(fileName) ?? AppPaths.InstallRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
    }
}
