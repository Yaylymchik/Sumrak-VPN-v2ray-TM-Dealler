using System.IO;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SumraX.App.ViewModels;
using SumraX.App.Views;
using SumraX.App.Services;
using SumraX.Core.Interfaces;
using SumraX.Core.Models;
using SumraX.Infrastructure;
using SumraX.Infrastructure.Regions;
using SumraX.Infrastructure.Security;
using SumraX.Infrastructure.Update;
using SumraX.Infrastructure.Vpn;
using SumraX.Localization;

namespace SumraX.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    public static IHost HostApp { get; private set; } = null!;
    public static bool RequestedShutdown { get; private set; }

    public static void RequestShutdown() => RequestedShutdown = true;

    protected override async void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(true, "Global\\SumraXVPN_2A8F4E1C", out var isFirst);
        if (!isFirst)
        {
            var earlyLoc = new LocalizationService();
            MessageBox.Show(earlyLoc.Get("AlreadyRunning"), earlyLoc.Get("AppTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var earlyLocalization = new LocalizationService();

        if (!IsRunningAsAdministrator())
        {
            MessageBox.Show(
                earlyLocalization.Get("AdminRequired"),
                earlyLocalization.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        BootstrapWindow? splash = null;
        var splashClosed = false;

        DispatcherUnhandledException += (_, args) =>
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Sumrak VPN", "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, $"crash-{DateTime.Now:yyyy-MM-dd}.log"),
                    $"[{DateTime.Now:HH:mm:ss}] {args.Exception}\n\n");
            }
            catch
            {
                // ignore logging failures
            }

            MessageBox.Show(
                args.Exception.InnerException?.Message ?? args.Exception.Message,
                earlyLocalization.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            try
            {
                var ex = args.ExceptionObject as Exception;
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Sumrak VPN", "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(
                    Path.Combine(dir, $"crash-{DateTime.Now:yyyy-MM-dd}.log"),
                    $"[{DateTime.Now:HH:mm:ss}] FATAL: {ex}\n\n");
            }
            catch
            {
                // ignore
            }
        };

        try
        {
            splash = new BootstrapWindow(earlyLocalization);
            splash.Show();
            splash.Activate();
            await PumpUiAsync();

            splash.SetPhase("PHASE 01 / CORE");
            splash.SetProgress(0.08);
            splash.AppendLog("> initializing host runtime...");
            await PumpUiAsync();

            HostApp = Host.CreateDefaultBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSumraXServices();
                    services.AddSingleton<MainViewModel>();
                    services.AddSingleton<HomeViewModel>();
                    services.AddSingleton<ServersViewModel>();
                    services.AddSingleton<SpeedViewModel>();
                    services.AddSingleton<SecurityViewModel>();
                    services.AddSingleton<SettingsViewModel>();
                    services.AddSingleton<AiViewModel>();
                    services.AddSingleton<TrayIconService>();
                })
                .Build();

            splash.SetProgress(0.18);
            splash.AppendLog("> dependency injection ready");
            splash.SetPhase("PHASE 02 / SERVICES");
            splash.SetStatus(earlyLocalization.Get("BootstrapLoadingServices"));
            await PumpUiAsync();

            await HostApp.StartAsync();
            splash.SetProgress(0.32);
            splash.AppendLog("> services online");
            await PumpUiAsync();

            var loc = HostApp.Services.GetRequiredService<ILocalizationService>();
            var settings = HostApp.Services.GetRequiredService<ISettingsRepository>();
            HostApp.Services.GetRequiredService<ISystemProxyManager>().CleanupStaleProxyOnStartup();
            TunAdapterCleanup.CleanupAllWintun();
            splash.AppendLog("> loading settings...");
            var appSettings = await settings.GetAsync();
            loc.SetLanguage(appSettings.Language);
            SumraX.App.Themes.ThemeService.Apply(appSettings.Theme);
            splash.AppendLog("> settings ready");

            if (appSettings.CheckConflictsOnStart)
            {
                var conflictService = HostApp.Services.GetRequiredService<IConflictDetectionService>();
                var conflicts = await conflictService.DetectAsync();
                foreach (var item in conflicts.Items.Where(i => i.Severity >= ConflictSeverity.Warning))
                {
                    splash.AppendLog($"> conflict: {item.Title}");
                }
            }

            if (!appSettings.OnboardingCompleted)
            {
                splash.SetPhase("SETUP / WELCOME");
                splash.SetStatus(loc.Get("OnboardingWelcomeTitle"));
                splash.AppendLog("> first launch setup...");
                await PumpUiAsync();

                splash.Hide();
                var onboarding = new OnboardingWindow(
                    loc,
                    settings,
                    HostApp.Services.GetRequiredService<IUserProfileRepository>(),
                    HostApp.Services.GetRequiredService<IRegionTimeService>());

                var onboardingOk = onboarding.ShowDialog() == true;
                splash.Show();
                splash.Activate();

                if (!onboardingOk)
                {
                    Shutdown();
                    return;
                }

                appSettings = await settings.GetAsync();
                loc.SetLanguage(appSettings.Language);
            }

            splash.SetPhase("PHASE 03 / XRAY");
            splash.SetProgress(0.38);
            splash.SetStatus(loc.Get("BootstrapInitializing"));
            splash.AppendLog("> verifying xray-core...");

            var bootstrap = HostApp.Services.GetRequiredService<IXrayBootstrap>();
            var xrayProgress = new Progress<string>(msg =>
            {
                splash.SetStatus(msg);
                splash.AppendLog($"> {msg}");
                splash.BumpProgress(0.05);
            });

            await bootstrap.EnsureAsync(xrayProgress);
            splash.SetProgress(0.72);
            splash.AppendLog("> xray-core verified");

            splash.SetPhase("PHASE 03B / SING-BOX");
            splash.AppendLog("> verifying sing-box (TUIC/SSR/Hysteria)...");
            var singBoxBootstrap = HostApp.Services.GetRequiredService<ISingBoxBootstrap>();
            var singBoxProgress = new Progress<string>(msg =>
            {
                splash.SetStatus(msg);
                splash.AppendLog($"> {msg}");
                splash.BumpProgress(0.03);
            });
            await singBoxBootstrap.EnsureAsync(singBoxProgress);
            splash.SetProgress(0.78);
            splash.AppendLog("> sing-box verified");

            var integrity = new BinaryIntegrityService();
            if (integrity.LoadManifest())
            {
                integrity.VerifyBundledCoresOrThrow();
                splash.AppendLog("> binary integrity OK");
            }

            splash.SetPhase("PHASE 04 / NETWORK");
            splash.SetProgress(0.85);
            splash.SetStatus(loc.Get("BootstrapLaunching"));
            splash.AppendLog("> starting network services...");

            var subRefresh = HostApp.Services.GetRequiredService<ISubscriptionRefreshService>();
            subRefresh.StartPeriodicRefresh();
            _ = Task.Run(async () =>
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
                    await subRefresh.RefreshOnStartupAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    NLog.LogManager.GetCurrentClassLogger().Warn("Subscription refresh on startup timed out");
                }
                catch (Exception ex)
                {
                    NLog.LogManager.GetCurrentClassLogger().Warn(ex, "Subscription refresh on startup failed");
                }
            });
            if (appSettings.AutoRefreshSubscriptions)
            {
                _ = subRefresh.RefreshIfDueAsync();

                // The subscription host may be unreachable without the tunnel (TUN mode has no
                // system proxy at all), so retry the refresh as soon as a connection is up.
                try
                {
                    var vpnEngine = HostApp.Services.GetRequiredService<IVpnEngine>();
                    vpnEngine.StatusChanged += (_, status) =>
                    {
                        if (status.State == SumraX.Core.Enums.ConnectionState.Connected)
                        {
                            _ = Task.Run(() => subRefresh.RefreshIfDueAsync());
                        }
                    };
                }
                catch (Exception ex)
                {
                    NLog.LogManager.GetCurrentClassLogger().Debug(ex, "Subscription refresh hook not attached");
                }
            }

            splash.SetProgress(0.92);
            splash.AppendLog("> loading secure interface...");
            await PumpUiAsync();

            async Task CloseSplashSafeAsync()
            {
                if (splashClosed || splash == null)
                {
                    return;
                }

                splashClosed = true;
                var s = splash;
                splash = null;
                s.SetProgress(1);
                s.SetStatus(loc.Get("BootstrapReady"));
                await s.CloseAnimatedAsync();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    var update = await new GitHubReleaseUpdateService().CheckAsync();
                    if (update.UpdateAvailable)
                    {
                        await Dispatcher.InvokeAsync(() =>
                            MessageBox.Show(
                                $"Доступна Sumrak VPN {update.LatestVersion} (сейчас {update.CurrentVersion}).\n{update.DetailUrl}",
                                earlyLocalization.Get("AppTitle"),
                                MessageBoxButton.OK,
                                MessageBoxImage.Information));
                    }
                }
                catch
                {
                    // optional
                }
            });

            var main = new MainWindow();
            var homeVm = HostApp.Services.GetRequiredService<HomeViewModel>();
            main.Loaded += async (_, _) =>
            {
                await CloseSplashSafeAsync();
                try
                {
                    await homeVm.RunStartupRoutineAsync();
                }
                catch
                {
                    // startup connect is best-effort
                }
            };

            var fallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            fallbackTimer.Tick += async (_, _) =>
            {
                fallbackTimer.Stop();
                await CloseSplashSafeAsync();
            };
            fallbackTimer.Start();

            splash.SetProgress(0.98);
            splash.AppendLog("> handshake complete.");
            main.Show();
            main.Activate();
            main.Focus();

            var tray = HostApp.Services.GetRequiredService<TrayIconService>();
            tray.Attach(main);
        }
        catch (Exception ex)
        {
            if (splash != null && !splashClosed)
            {
                splash.Close();
            }

            ILocalizationService loc;
            try { loc = HostApp.Services.GetRequiredService<ILocalizationService>(); }
            catch { loc = earlyLocalization; }

            var detail = ex.InnerException?.Message ?? ex.Message;
            NLog.LogManager.GetCurrentClassLogger().Error(ex, "Startup failed");
            MessageBox.Show(
                loc.Format("StartupFailed", detail),
                loc.Get("AppTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static Task PumpUiAsync() =>
        Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render).Task;

    protected override async void OnExit(ExitEventArgs e)
    {
        var vpn = HostApp?.Services.GetService<IVpnEngine>();
        if (vpn != null)
        {
            await vpn.DisconnectAsync();
        }

        var killSwitch = HostApp?.Services.GetService<IKillSwitchService>();
        if (killSwitch?.IsEnabled == true)
        {
            await killSwitch.DisableAsync();
        }

        if (HostApp != null)
        {
            await HostApp.StopAsync();
            HostApp.Dispose();
        }

        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();

        try
        {
            HostApp?.Services.GetService<TrayIconService>()?.Dispose();
        }
        catch
        {
            // ignore tray cleanup errors
        }

        base.OnExit(e);
    }

    private static bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
