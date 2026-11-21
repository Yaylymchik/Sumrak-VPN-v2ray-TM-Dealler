using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using SumraX.Core;
using SumraX.Core.Enums;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure;
using SumraX.Infrastructure.Data;
using SumraX.Infrastructure.Security;
using SumraX.Infrastructure.Vpn;

// Reproduces the real refresh pipeline against a COPY of the app data root.
var realRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppPaths.AppFolderName);
var tempRoot = Path.Combine(Path.GetTempPath(), "sumrax_refresh_probe_" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(tempRoot);
foreach (var file in Directory.GetFiles(realRoot))
{
    File.Copy(file, Path.Combine(tempRoot, Path.GetFileName(file)));
}

AppPaths.OverrideDataRoot(tempRoot);
Console.WriteLine($"probe root      : {tempRoot}");

var secure = new DpapiSecureStorage();
var settingsRepo = new JsonSettingsRepository(secure);
var serverRepo = new SqliteServerRepository(secure);
var loc = new StubLoc();
var import = new ConfigImportService(loc, settingsRepo);
var svc = new SubscriptionRefreshService(settingsRepo, serverRepo, import, loc);

var before = await serverRepo.GetAllAsync();
var settings = await settingsRepo.GetAsync();
var url = settings.SubscriptionUrls.FirstOrDefault() ?? "";
Console.WriteLine($"url host        : {(string.IsNullOrEmpty(url) ? "<none>" : new Uri(url).Host)}");
Console.WriteLine($"url len         : {url.Length}");
Console.WriteLine($"auto-refresh    : {settings.AutoRefreshSubscriptions}, interval={settings.SubscriptionRefreshIntervalHours}h, onStart={settings.UpdateSubscriptionsOnStart}");
Console.WriteLine($"purge           : {settings.PurgeOldProfilesOnSubscriptionUpdate}, ignoreDup={settings.IgnoreDuplicateServers}");
Console.WriteLine($"last refresh    : {settings.LastSubscriptionRefreshUtc:o}");
Console.WriteLine($"servers before  : {before.Count} (tagged {before.Count(s => !string.IsNullOrWhiteSpace(s.SubscriptionUrl))})");

var sw = Stopwatch.StartNew();
var res = await svc.RefreshUrlAsync(url);
sw.Stop();

var after = await serverRepo.GetAllAsync();
Console.WriteLine($"elapsed ms      : {sw.ElapsedMilliseconds}");
Console.WriteLine($"updated/saved   : {res.UpdatedServers}");
Console.WriteLine($"removed         : {res.RemovedServers}");
Console.WriteLine($"skipped         : {res.Skipped}");
Console.WriteLine($"errors          : {(res.Errors.Count == 0 ? "<none>" : string.Join(" | ", res.Errors))}");
Console.WriteLine($"servers after   : {after.Count}");

try { Directory.Delete(tempRoot, true); } catch { }

internal sealed class StubLoc : ILocalizationService
{
    public AppLanguage CurrentLanguage => AppLanguage.Russian;
    public event EventHandler? LanguageChanged;
    public string Get(string key) => key;
    public string Format(string key, params object[] args) => key + "(" + string.Join(",", args) + ")";
    public void SetLanguage(AppLanguage language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
}
