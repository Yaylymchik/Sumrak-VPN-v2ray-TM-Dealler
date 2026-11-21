using System.Reflection;
using System.Windows;
using System.Windows.Markup;
using Microsoft.Extensions.DependencyInjection;
using SumraX.Core.Interfaces;

namespace SumraX.App.Markup;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = string.Empty;

    public LocExtension() { }

    public LocExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (Application.Current is App && App.HostApp != null)
        {
            return App.HostApp.Services.GetService(typeof(ILocalizationService)) is ILocalizationService loc
                ? loc.Get(Key)
                : Key;
        }
        return Key;
    }
}

public static class AppInfo
{
    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static string ProductName => "Sumrak VPN";
}
