using System.Globalization;
using System.Windows.Data;
using Microsoft.Extensions.DependencyInjection;
using SumraX.Core.Interfaces;

namespace SumraX.App.Converters;

public sealed class PingMsDisplayConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var ms = value switch
        {
            int i => i,
            long l => (int)l,
            _ => -1
        };

        var loc = App.HostApp.Services.GetRequiredService<ILocalizationService>();
        return ms > 0 ? loc.Format("PingMsFormat", ms) : loc.Get("PingNa");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}