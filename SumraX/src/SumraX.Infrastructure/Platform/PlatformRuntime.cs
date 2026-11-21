using System.Runtime.InteropServices;

namespace SumraX.Infrastructure.Platform;

public static class PlatformRuntime
{
    public static bool IsWindows => OperatingSystem.IsWindows();
    public static bool IsMacOS => OperatingSystem.IsMacOS();
    public static bool IsLinux => OperatingSystem.IsLinux();

    public static bool IsArm64 =>
        RuntimeInformation.ProcessArchitecture is Architecture.Arm64;

    public static string XrayBinaryName => IsWindows ? "xray.exe" : "xray";
    public static string SingBoxBinaryName => IsWindows ? "sing-box.exe" : "sing-box";
    public static string Tun2ProxyBinaryName => IsWindows ? "tun2proxy-bin.exe" : "tun2proxy-bin";

    public static string XrayAssetPattern => IsWindows
        ? "windows-64"
        : IsArm64 ? "macos-arm64" : "macos-64";

    public static string SingBoxAssetPattern => IsWindows
        ? "windows-amd64"
        : IsArm64 ? "darwin-arm64" : "darwin-amd64";

    public static void EnsureExecutable(string path)
    {
        if (!File.Exists(path) || IsWindows)
        {
            return;
        }

        if (!IsMacOS && !IsLinux)
        {
            return;
        }

#if !WINDOWS
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(path, mode);
#endif
    }
}
