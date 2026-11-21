namespace SumraX.Infrastructure;

public static class AppPaths
{
    public const string AppFolderName = "Sumrak VPN";
    private const string LegacyAppFolderName = "SumraX VPN";
    public const string ConfigFile = "config.json";
    public const string SingBoxConfigFile = "sing-box.json";
    public const string SingBoxTunConfigFile = "sing-box-tun.json";
    public const string DatabaseFile = "sumrax.db";
    public const string SettingsFile = "settings.json";
    public const string BackupFolder = "backups";
    public const string LogsFolder = "logs";

    private static string? _dataRootOverride;

    public static string XrayFolder => Path.Combine("bin", "xray");
    public static string SingBoxFolder => Path.Combine("bin", "sing-box");
    public static string Tun2Folder => Path.Combine("bin", "tun2");

    public static string DataRoot =>
        _dataRootOverride ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName);

    /// <summary>Test helper. Pass null to clear override. Returns previous value.</summary>
    public static string? OverrideDataRoot(string? path)
    {
        var previous = _dataRootOverride;
        _dataRootOverride = path;
        return previous;
    }

    public static string InstallRoot => AppContext.BaseDirectory;

    public static string XrayPath => Path.Combine(InstallRoot, XrayFolder, Platform.PlatformRuntime.XrayBinaryName);
    public static string SingBoxPath => Path.Combine(InstallRoot, SingBoxFolder, Platform.PlatformRuntime.SingBoxBinaryName);
    public static string Tun2ProxyPath => Path.Combine(InstallRoot, Tun2Folder, Platform.PlatformRuntime.Tun2ProxyBinaryName);
    public static string ConfigPath => Path.Combine(DataRoot, ConfigFile);
    public static string SingBoxConfigPath => Path.Combine(DataRoot, SingBoxConfigFile);
    public static string SingBoxTunConfigPath => Path.Combine(DataRoot, SingBoxTunConfigFile);
    public static string DatabasePath => Path.Combine(DataRoot, DatabaseFile);
    public static string SettingsPath => Path.Combine(DataRoot, SettingsFile);
    public static string BackupRoot => Path.Combine(DataRoot, BackupFolder);
    public static string LogsRoot => Path.Combine(DataRoot, LogsFolder);

    public static void EnsureDirectories()
    {
        MigrateLegacyDataRootIfNeeded();
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(BackupRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(Path.Combine(InstallRoot, XrayFolder));
        Directory.CreateDirectory(Path.Combine(InstallRoot, SingBoxFolder));
        Directory.CreateDirectory(Path.Combine(InstallRoot, Tun2Folder));
    }

    private static void MigrateLegacyDataRootIfNeeded()
    {
        if (_dataRootOverride is not null)
        {
            return;
        }

        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var legacy = Path.Combine(appData, LegacyAppFolderName);
        var current = Path.Combine(appData, AppFolderName);
        if (!Directory.Exists(legacy) || Directory.Exists(current))
        {
            return;
        }

        try
        {
            Directory.Move(legacy, current);
        }
        catch
        {
            // Keep using new folder; user can copy manually if move fails.
        }
    }
}
