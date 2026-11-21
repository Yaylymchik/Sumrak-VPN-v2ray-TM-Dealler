namespace SumraX.Infrastructure.Platform;

public static class PlatformAdmin
{
    public static bool IsElevated()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            return Environment.GetEnvironmentVariable("SUDO_UID") != null || Environment.UserName == "root";
        }

        return false;
    }
}
