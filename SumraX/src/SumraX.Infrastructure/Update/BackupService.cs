using SumraX.Core.Interfaces;

namespace SumraX.Infrastructure.Update;

public sealed class BackupService : IBackupService
{
    public async Task BackupAsync()
    {
        AppPaths.EnsureDirectories();
        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var target = Path.Combine(AppPaths.BackupRoot, stamp);
        Directory.CreateDirectory(target);

        foreach (var file in new[] { AppPaths.SettingsPath, AppPaths.DatabasePath, AppPaths.ConfigPath })
        {
            if (File.Exists(file))
            {
                CopyWithRetry(file, Path.Combine(target, Path.GetFileName(file)));
            }
        }

        await Task.CompletedTask;
    }

    public async Task RestoreLatestAsync()
    {
        if (!Directory.Exists(AppPaths.BackupRoot))
        {
            return;
        }

        var latest = Directory.GetDirectories(AppPaths.BackupRoot)
            .OrderByDescending(d => d)
            .FirstOrDefault();

        if (latest == null)
        {
            return;
        }

        foreach (var file in Directory.GetFiles(latest))
        {
            CopyWithRetry(file, Path.Combine(AppPaths.DataRoot, Path.GetFileName(file)));
        }

        await Task.CompletedTask;
    }

    private static void CopyWithRetry(string source, string dest, int attempts = 5)
    {
        for (var i = 0; i < attempts; i++)
        {
            try
            {
                File.Copy(source, dest, true);
                return;
            }
            catch (IOException) when (i < attempts - 1)
            {
                Thread.Sleep(150);
            }
        }
    }
}
