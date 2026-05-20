using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Возвращает (count - N), но не меньше 0. ConverterParameter = N (default 3).
/// Используется для текста «+M» на overflow-пилюле.
/// </summary>
public sealed class TagsOverflowCountConverter : IValueConverter
{
    public object Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        int n = 3;
        if (parameter is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            n = parsed;
        int count = 0;
        if (value is IEnumerable seq)
        {
            foreach (var _ in seq) count++;
        }
        return System.Math.Max(0, count - n);
    }

    public object ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
