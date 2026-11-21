using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SumraX.App.Desktop.ViewModels;
using SumraX.Core.Interfaces;
using SumraX.Infrastructure;
using SumraX.Infrastructure.Security;
using SumraX.Infrastructure.Vpn;

namespace SumraX.App.Desktop;

public partial class App : Application
{
    public static IHost HostApp { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override async void OnFrameworkInitializationCompleted()
    {
        HostApp = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSumraXServices();
                services.AddSingleton<DesktopMainViewModel>();
            })
            .Build();

        await HostApp.StartAsync();

        var loc = HostApp.Services.GetRequiredService<ILocalizationService>();
        var settings = HostApp.Services.GetRequiredService<ISettingsRepository>();
        HostApp.Services.GetRequiredService<ISystemProxyManager>().CleanupStaleProxyOnStartup();

        var appSettings = await settings.GetAsync();
        loc.SetLanguage(appSettings.Language);

        await HostApp.Services.GetRequiredService<IXrayBootstrap>().EnsureAsync();
        await HostApp.Services.GetRequiredService<ISingBoxBootstrap>().EnsureAsync();

        var integrity = new BinaryIntegrityService();
        if (integrity.LoadManifest())
        {
            integrity.VerifyBundledCoresOrThrow();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = HostApp.Services.GetRequiredService<DesktopMainViewModel>()
            };

            desktop.Exit += async (_, _) =>
            {
                var vpn = HostApp.Services.GetService<IVpnEngine>();
                if (vpn != null)
                {
                    await vpn.DisconnectAsync();
                }

                await HostApp.StopAsync();
                HostApp.Dispose();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
