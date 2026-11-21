using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using SumraX.Core.Enums;
using SumraX.Core.Models;

namespace SumraX.App.Converters;

public sealed class CountryFlagConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var code = value switch
        {
            ServerProfile s => s.CountryCode,
            string str => str,
            _ => ""
        };
        return ToFlag(code);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    internal static string ToFlag(string? code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != 2 || code.Equals("UN", StringComparison.OrdinalIgnoreCase))
        {
            return "🌍";
        }
        code = code.ToUpperInvariant();
        return string.Concat(code.Select(c => char.ConvertFromUtf32(0x1F1E6 + (c - 'A'))));
    }
}

public sealed class FavoriteIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "★" : "☆";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToConnectButtonStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var connected = value is ConnectionState.Connected;
        var key = connected ? "DisconnectButton" : "ConnectButton";
        return Application.Current.FindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ConnectionStateToStatusBadgeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is ConnectionState.Connected ? "StatusBadgeProtected" : "StatusBadgeUnprotected";
        return Application.Current.FindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ConnectionStateToStatusTextBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ConnectionState.Connected
            ? new SolidColorBrush(Color.FromRgb(0, 255, 136))
            : value is ConnectionState.Connecting or ConnectionState.Reconnecting
                ? new SolidColorBrush(Color.FromRgb(0, 168, 255))
                : new SolidColorBrush(Color.FromRgb(255, 51, 102));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ChatBubbleStyleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var key = value is true ? "ChatBubbleUser" : "ChatBubbleAi";
        return Application.Current.FindResource(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var color = value is ConnectionState state ? state switch
        {
            ConnectionState.Connected => Color.FromRgb(16, 185, 129),
            ConnectionState.Connecting or ConnectionState.Reconnecting => Color.FromRgb(245, 158, 11),
            ConnectionState.Error => Color.FromRgb(239, 68, 68),
            _ => Color.FromRgb(100, 116, 139)
        } : Color.FromRgb(100, 116, 139);
        return new SolidColorBrush(color);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ProgressSegmentBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Active = new(Color.FromRgb(0, 255, 136));
    private static readonly SolidColorBrush Inactive = new(Color.FromRgb(14, 21, 32));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var progress = value is int p ? p : 0;
        var segment = int.TryParse(parameter?.ToString(), out var s) ? s : 1;
        return progress >= segment ? Active : Inactive;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}

public sealed class StringNotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s && !string.IsNullOrWhiteSpace(s) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class LocFormatConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (parameter is not string key || Application.Current is not App || App.HostApp == null)
        {
            return value?.ToString() ?? string.Empty;
        }

        if (App.HostApp.Services.GetService(typeof(SumraX.Core.Interfaces.ILocalizationService))
            is not SumraX.Core.Interfaces.ILocalizationService loc)
        {
            return value?.ToString() ?? string.Empty;
        }

        return loc.Format(key, value ?? 0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class LanguageDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SumraX.Core.Enums.AppLanguage lang || Application.Current is not App || App.HostApp == null)
        {
            return value?.ToString() ?? string.Empty;
        }

        if (App.HostApp.Services.GetService(typeof(SumraX.Core.Interfaces.ILocalizationService))
            is not SumraX.Core.Interfaces.ILocalizationService loc)
        {
            return value?.ToString() ?? string.Empty;
        }

        return SumraX.Localization.LocalizationService.GetLanguageName(lang, loc.CurrentLanguage);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ThemeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not SumraX.Core.Enums.AppTheme theme)
        {
            return value?.ToString() ?? string.Empty;
        }

        if (Application.Current is App && App.HostApp?.Services.GetService(typeof(SumraX.Core.Interfaces.ILocalizationService))
            is SumraX.Core.Interfaces.ILocalizationService loc)
        {
            return theme switch
            {
                SumraX.Core.Enums.AppTheme.Midnight => loc.Get("ThemeMidnight"),
                SumraX.Core.Enums.AppTheme.Emerald => loc.Get("ThemeEmerald"),
                SumraX.Core.Enums.AppTheme.Ember => loc.Get("ThemeEmber"),
                SumraX.Core.Enums.AppTheme.Arctic => loc.Get("ThemeArctic"),
                _ => loc.Get("ThemeCyber")
            };
        }

        return theme.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class IntEqualsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is int i && int.TryParse(parameter?.ToString(), out var target) && i == target;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ExpandChevronConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? "▲" : "▼";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TunStackModeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Application.Current is App && App.HostApp != null &&
            App.HostApp.Services.GetService(typeof(SumraX.Core.Interfaces.ILocalizationService))
                is SumraX.Core.Interfaces.ILocalizationService loc)
        {
            return value switch
            {
                TunStackMode.System => loc.Get("TunStackSystem"),
                TunStackMode.GVisor => loc.Get("TunStackGVisor"),
                _ => loc.Get("TunStackMixed")
            };
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class TunProviderDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Application.Current is App && App.HostApp != null &&
            App.HostApp.Services.GetService(typeof(SumraX.Core.Interfaces.ILocalizationService))
                is SumraX.Core.Interfaces.ILocalizationService loc)
        {
            return value switch
            {
                TunProvider.SumraXTun => loc.Get("TunProviderSumraX"),
                TunProvider.Tun2Proxy => loc.Get("TunProviderTun2Proxy"),
                _ => loc.Get("TunProviderSingBox")
            };
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class PingTypeDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Application.Current is App && App.HostApp != null &&
            App.HostApp.Services.GetService(typeof(SumraX.Core.Interfaces.ILocalizationService))
                is SumraX.Core.Interfaces.ILocalizationService loc)
        {
            return value switch
            {
                PingType.Icmp => loc.Get("PingTypeIcmp"),
                PingType.Proxy => loc.Get("PingTypeProxy"),
                PingType.ProxyHead => loc.Get("PingTypeProxyHead"),
                PingType.Tcp => loc.Get("PingTypeTcp"),
                _ => loc.Get("PingTypeProxy")
            };
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class ClientUserAgentDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not ClientUserAgentPreset preset || Application.Current is not App || App.HostApp == null)
        {
            return value?.ToString() ?? string.Empty;
        }

        if (App.HostApp.Services.GetService(typeof(SumraX.Core.Interfaces.ILocalizationService))
            is not SumraX.Core.Interfaces.ILocalizationService loc)
        {
            return value?.ToString() ?? string.Empty;
        }

        var key = preset switch
        {
            ClientUserAgentPreset.ChromeAndroid => "UserAgentChromeAndroid",
            ClientUserAgentPreset.SafariMacOS => "UserAgentSafariMacOS",
            ClientUserAgentPreset.SafariIOS => "UserAgentSafariIOS",
            ClientUserAgentPreset.FirefoxWindows => "UserAgentFirefoxWindows",
            ClientUserAgentPreset.EdgeWindows => "UserAgentEdgeWindows",
            _ => "UserAgentChromeWindows"
        };

        return loc.Get(key);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class BoolToSpeakerConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (Application.Current is not App || App.HostApp == null)
        {
            return string.Empty;
        }

        if (App.HostApp.Services.GetService(typeof(SumraX.Core.Interfaces.ILocalizationService))
            is not SumraX.Core.Interfaces.ILocalizationService loc)
        {
            return string.Empty;
        }

        return value is true ? loc.Get("UserSpeaker") : loc.Get("AiSpeaker");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
