using System.Text.Json;
using System.Text.Json.Serialization;
using SumraX.Core.Enums;
using SumraX.Core.Models;
using SumraX.Infrastructure;
using SumraX.Infrastructure.Data;
using SumraX.Infrastructure.Security;
using Xunit;

namespace SumraX.Infrastructure.Tests;

public class JsonSettingsRepositoryTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _previousOverride;

    public JsonSettingsRepositoryTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "SumraXSettingsTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _previousOverride = AppPaths.OverrideDataRoot(_tempRoot);
    }

    public void Dispose()
    {
        AppPaths.OverrideDataRoot(_previousOverride);
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [Fact]
    public async Task Save_Then_Get_Preserves_Theme_Language_And_SubscriptionUrls()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repo = new JsonSettingsRepository(new DpapiSecureStorage());
        var settings = await repo.GetAsync();
        settings.Theme = AppTheme.Emerald;
        settings.Language = AppLanguage.English;
        settings.AutoStart = true;
        settings.KillSwitch = true;
        settings.UseTunMode = false;
        settings.SubscriptionUrls =
        [
            "https://example.com/sub1",
            "https://example.com/sub2"
        ];
        settings.SubscriptionRefreshIntervalHours = 12;
        await repo.SaveAsync(settings);

        repo.ClearCache();
        var loaded = await repo.GetAsync();

        Assert.Equal(AppTheme.Emerald, loaded.Theme);
        Assert.Equal(AppLanguage.English, loaded.Language);
        Assert.True(loaded.AutoStart);
        Assert.True(loaded.KillSwitch);
        Assert.False(loaded.UseTunMode);
        Assert.Equal(12, loaded.SubscriptionRefreshIntervalHours);
        Assert.Equal(2, loaded.SubscriptionUrls.Count);
        Assert.Contains("https://example.com/sub1", loaded.SubscriptionUrls);
        Assert.Contains("https://example.com/sub2", loaded.SubscriptionUrls);
        Assert.True(File.Exists(AppPaths.SettingsPath));
    }

    [Fact]
    public async Task Register_Style_Mutation_Persists_Urls_Across_Reload()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repo = new JsonSettingsRepository(new DpapiSecureStorage());
        var settings = await repo.GetAsync();
        settings.SubscriptionUrls.Add("https://provider.test/feed");
        await repo.SaveAsync(settings);

        // Simulate Settings auto-save mutating other fields without touching URLs.
        var cached = await repo.GetAsync();
        cached.MinimizeToTray = false;
        cached.Theme = AppTheme.Arctic;
        await repo.SaveAsync(cached);

        repo.ClearCache();
        var loaded = await repo.GetAsync();
        Assert.Contains("https://provider.test/feed", loaded.SubscriptionUrls);
        Assert.False(loaded.MinimizeToTray);
        Assert.Equal(AppTheme.Arctic, loaded.Theme);
    }

    [Fact]
    public void Dpapi_Roundtrip_Json_With_Enums_As_Strings()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() }
        };
        var storage = new DpapiSecureStorage();
        var json = JsonSerializer.Serialize(new AppSettings
        {
            Theme = AppTheme.Ember,
            Language = AppLanguage.Russian,
            SubscriptionUrls = ["https://a.test/x"]
        }, options);

        var encrypted = storage.Encrypt(json);
        var decrypted = storage.Decrypt(encrypted);
        var loaded = JsonSerializer.Deserialize<AppSettings>(decrypted, options);

        Assert.NotNull(loaded);
        Assert.Equal(AppTheme.Ember, loaded!.Theme);
        Assert.Equal(["https://a.test/x"], loaded.SubscriptionUrls);
    }

    [Fact]
    public async Task Corrupt_Primary_Restores_From_Bak()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var storage = new DpapiSecureStorage();
        var repo = new JsonSettingsRepository(storage);
        var settings = await repo.GetAsync();
        settings.SubscriptionUrls = ["https://keep.me/sub"];
        settings.Theme = AppTheme.Midnight;
        await repo.SaveAsync(settings);

        // Force a bak by saving again, then corrupt primary.
        settings.MinimizeToTray = false;
        await repo.SaveAsync(settings);
        await File.WriteAllTextAsync(AppPaths.SettingsPath, "NOT-VALID-DPAPI-OR-JSON");

        repo.ClearCache();
        var loaded = await repo.GetAsync();
        Assert.Contains("https://keep.me/sub", loaded.SubscriptionUrls);
        Assert.Equal(AppTheme.Midnight, loaded.Theme);
    }

    [Fact]
    public async Task Corrupt_Primary_And_Bak_Quarantines_On_Save()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        AppPaths.EnsureDirectories();
        await File.WriteAllTextAsync(AppPaths.SettingsPath, "BROKEN");
        await File.WriteAllTextAsync(AppPaths.SettingsPath + ".bak", "BROKEN-BAK");

        var repo = new JsonSettingsRepository(new DpapiSecureStorage());
        var degraded = await repo.GetAsync();
        Assert.Empty(degraded.SubscriptionUrls);

        degraded.SubscriptionUrls.Add("https://new.sub/after-corrupt");
        await repo.SaveAsync(degraded);

        Assert.True(File.Exists(AppPaths.SettingsPath));
        Assert.True(Directory.GetFiles(_tempRoot, "settings.json.corrupt-*").Length >= 1);

        repo.ClearCache();
        var loaded = await repo.GetAsync();
        Assert.Contains("https://new.sub/after-corrupt", loaded.SubscriptionUrls);
    }
}
