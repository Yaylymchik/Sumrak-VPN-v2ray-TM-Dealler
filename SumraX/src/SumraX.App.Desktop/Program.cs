using Avalonia;
using SumraX.Infrastructure;

namespace SumraX.App.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppPaths.EnsureDirectories();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
