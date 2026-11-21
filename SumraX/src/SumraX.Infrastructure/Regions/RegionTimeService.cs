using System.ComponentModel;
using System.Diagnostics;
using SumraX.Infrastructure.Security;

using SumraX.Infrastructure.Platform;

namespace SumraX.Infrastructure.Regions;

public interface IRegionTimeService
{
    Task<bool> ApplyRegionAsync(string regionCode, CancellationToken cancellationToken = default);
}

public sealed class RegionTimeService : IRegionTimeService
{
    public Task<bool> ApplyRegionAsync(string regionCode, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(false);
        }

        return ApplyRegionWindowsAsync(regionCode, cancellationToken);
    }

    private static async Task<bool> ApplyRegionWindowsAsync(string regionCode, CancellationToken cancellationToken)
    {
        var region = RegionCatalog.Find(regionCode);
        if (region == null)
        {
            return false;
        }

        try
        {
            var tzOk = await RunTzUtilAsync(region.WindowsTimeZoneId, cancellationToken);
            await TryResyncTimeAsync(cancellationToken);
            return tzOk;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> RunTzUtilAsync(string timeZoneId, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = TrustedBinaryGuard.GetSystem32Binary("tzutil.exe"),
            Arguments = $"/s \"{timeZoneId}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return false;
            }

            await proc.WaitForExitAsync(cancellationToken);
            return proc.ExitCode == 0;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return false;
        }
    }

    private static async Task TryResyncTimeAsync(CancellationToken cancellationToken)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = TrustedBinaryGuard.GetSystem32Binary("w32tm.exe"),
                Arguments = "/resync",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(cancellationToken);
            }
        }
        catch
        {
            // time sync optional
        }
    }
}
