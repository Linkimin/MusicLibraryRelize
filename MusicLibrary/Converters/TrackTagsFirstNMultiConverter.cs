using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Converters;

/// <summary>
/// MultiBinding: (trackId, TagsByTrackId-словарь) → первые N тегов трека (snapshot, IReadOnlyList).
/// ConverterParameter — N (default 3). Snapshot, не live — добавление тега за позицию N
/// не отразится на карточке до перевыделения трека. Сознательное ограничение MVP.
/// </summary>
public sealed class TrackTagsFirstNMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2) return System.Array.Empty<Tag>();
        if (values[0] is not int trackId) return System.Array.Empty<Tag>();
        if (values[1] is not IReadOnlyDictionary<int, System.Collections.ObjectModel.ObservableCollection<Tag>> dict)
            return System.Array.Empty<Tag>();

        int n = 3;
        if (parameter is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            n = parsed;

        return dict.TryGetValue(trackId, out var coll)
            ? coll.Take(n).ToList()
            : (object)System.Array.Empty<Tag>();
    }

    public object[] ConvertBack(object value, System.Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
