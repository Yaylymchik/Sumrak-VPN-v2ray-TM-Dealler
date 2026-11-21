using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SumraX.App.Converters;

public sealed class ServerSelectedConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not string serverId || values[1] is not string selectedId)
        {
            return Visibility.Collapsed;
        }

        var selected = !string.IsNullOrWhiteSpace(serverId) &&
                       string.Equals(serverId, selectedId, StringComparison.Ordinal);
        return selected ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}