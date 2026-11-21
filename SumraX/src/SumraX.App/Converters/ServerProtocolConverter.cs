using System.Globalization;
using System.Windows.Data;
using SumraX.Core.Models;

namespace SumraX.App.Converters;

public sealed class ServerProtocolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is ServerProfile profile ? ServerProtocolFormatter.Format(profile) : "";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}