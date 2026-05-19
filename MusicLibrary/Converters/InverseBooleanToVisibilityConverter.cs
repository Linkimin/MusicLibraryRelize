using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Инверсия BooleanToVisibilityConverter: true → Collapsed, false → Visible.
/// Используется для empty-state блоков (показать «список пуст», когда HasX=false).
/// </summary>
public sealed class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool flag = value is bool b && b;
        return flag ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}
