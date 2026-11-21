using Microsoft.Extensions.DependencyInjection;
using SumraX.Core.Interfaces;
using SumraX.Infrastructure.Ai;
using SumraX.Infrastructure.Data;
using SumraX.Infrastructure.Diagnostics;
using SumraX.Infrastructure.Logging;
using SumraX.Infrastructure.Network;
using SumraX.Infrastructure.Regions;
using SumraX.Infrastructure.Security;
using SumraX.Infrastructure.Update;
using SumraX.Infrastructure.Vpn;
using SumraX.Localization;

namespace SumraX.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSumraXServices(this IServiceCollection services)
    {
        LogBootstrap.Configure();

        services.AddSingleton<ISecureStorage, DpapiSecureStorage>();
        services.AddSingleton<ISettingsRepository, JsonSettingsRepository>();
        services.AddSingleton<IUserProfileRepository, UserProfileRepository>();
        services.AddSingleton<IServerRepository, SqliteServerRepository>();
        services.AddSingleton<IVpnEngine, VpnEngine>();
        services.AddSingleton<IConfigImportService, ConfigImportService>();
        services.AddSingleton<ISubscriptionRefreshService, SubscriptionRefreshService>();
        services.AddSingleton<ISpeedTestService, SpeedTestService>();
        services.AddSingleton<IIpInfoService, IpInfoService>();
        services.AddSingleton<INetworkMonitor, NetworkMonitor>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IAiAssistant, AiAssistantService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IXrayBootstrap, XrayBootstrap>();
        services.AddSingleton<ISingBoxBootstrap, SingBoxBootstrap>();
        services.AddSingleton<IRegionTimeService, RegionTimeService>();
        services.AddSingleton<QrImportService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
        services.AddSingleton<IDebugExportService, DebugExportService>();
        services.AddSingleton<IConflictDetectionService, ConflictDetectionService>();
        services.AddSingleton<ILogViewerService, LogViewerService>();

        if (OperatingSystem.IsWindows())
        {
            services.AddSingleton<ISystemProxyManager, WindowsSystemProxyManager>();
            services.AddSingleton<IKillSwitchService, WindowsKillSwitchService>();
            services.AddSingleton<IAutoStartService, WindowsAutoStartService>();
        }
        else if (OperatingSystem.IsMacOS())
        {
            services.AddSingleton<ISystemProxyManager, MacSystemProxyManager>();
            services.AddSingleton<IKillSwitchService, MacKillSwitchService>();
            services.AddSingleton<IAutoStartService, MacAutoStartService>();
        }
        else
        {
            services.AddSingleton<ISystemProxyManager, MacSystemProxyManager>();
            services.AddSingleton<IKillSwitchService, MacKillSwitchService>();
            services.AddSingleton<IAutoStartService, MacAutoStartService>();
        }

        return services;
    }
}
