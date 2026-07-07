using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// int → Visibility (0 = Collapsed, иначе Visible).
/// ConverterParameter="Invert" переворачивает логику (0 = Visible, иначе Collapsed) —
/// используется для empty-state плейсхолдеров (см. AlbumsArtistsTemplates.xaml).
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasItems = value is int n && n > 0;
        bool invert = string.Equals(parameter as string, "Invert", StringComparison.OrdinalIgnoreCase);
        bool visible = invert ? !hasItems : hasItems;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
