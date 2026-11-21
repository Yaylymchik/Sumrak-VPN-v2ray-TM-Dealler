using NLog;
using NLog.Config;
using NLog.Targets;

namespace SumraX.Infrastructure.Logging;

public static class LogBootstrap
{
    public static void Configure()
    {
        AppPaths.EnsureDirectories();
        var config = new LoggingConfiguration();
        var fileTarget = new FileTarget("file")
        {
            FileName = Path.Combine(AppPaths.LogsRoot, "sumrax-${shortdate}.log"),
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message}${onexception:inner=${exception:format=tostring}}",
            ArchiveEvery = FileArchivePeriod.Day,
            MaxArchiveFiles = 14
        };
        config.AddTarget(fileTarget);
        config.AddRule(LogLevel.Debug, LogLevel.Fatal, fileTarget);
        LogManager.Configuration = config;
    }
}
