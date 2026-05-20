using System.Collections;
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>
/// Берёт IEnumerable (обычно &lt;Tag&gt;), возвращает первые N (ConverterParameter = "N").
/// Default N = 3. null → пустая последовательность.
/// </summary>
public sealed class TagsToFirstNConverter : IValueConverter
{
    public object Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not IEnumerable seq) return System.Array.Empty<object>();
        int n = 3;
        if (parameter is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            n = parsed;
        var result = new System.Collections.Generic.List<object>(n);
        int count = 0;
        foreach (var item in seq)
        {
            if (count >= n) break;
            result.Add(item);
            count++;
        }
        return result;
    }

    public object ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
