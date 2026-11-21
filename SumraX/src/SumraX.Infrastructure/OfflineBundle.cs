namespace SumraX.Infrastructure;

/// <summary>
/// Detects fully bundled offline installs (no runtime downloads).
/// </summary>
public static class OfflineBundle
{
    public const string MarkerFileName = ".sumrax-offline-bundle";

    public static bool IsBundled =>
        File.Exists(Path.Combine(AppPaths.InstallRoot, MarkerFileName));

    public static void EnsureCorePresent(string corePath, string coreLabel)
    {
        if (!IsBundled || File.Exists(corePath))
        {
            return;
        }

        throw new FileNotFoundException(
            $"{coreLabel} is missing from the offline bundle. Reinstall Sumrak VPN.",
            corePath);
    }
}
