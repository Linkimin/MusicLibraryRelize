using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Converters;

/// <summary>
/// MultiBinding: (trackId, TagsByTrackId-словарь) → количество скрытых тегов (count - N, ≥ 0).
/// ConverterParameter — N (default 3). Используется для текста "+M" в overflow-пилюле.
/// </summary>
public sealed class TrackTagsOverflowCountMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2) return 0;
        if (values[0] is not int trackId) return 0;
        if (values[1] is not IReadOnlyDictionary<int, System.Collections.ObjectModel.ObservableCollection<Tag>> dict)
            return 0;

        int n = 3;
        if (parameter is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            n = parsed;

        int count = dict.TryGetValue(trackId, out var coll) ? coll.Count : 0;
        return System.Math.Max(0, count - n);
    }

    public object[] ConvertBack(object value, System.Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
