using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Converters;

/// <summary>
/// MultiBinding: (trackId, TagsByTrackId-словарь) → Visibility (Visible если count > N, иначе Collapsed).
/// ConverterParameter — N (default 3). Скрывает overflow-пилюлю когда теги полностью помещаются.
/// </summary>
public sealed class TrackTagsOverflowVisibilityMultiConverter : IMultiValueConverter
{
    public object Convert(object[] values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2) return Visibility.Collapsed;
        if (values[0] is not int trackId) return Visibility.Collapsed;
        if (values[1] is not IReadOnlyDictionary<int, System.Collections.ObjectModel.ObservableCollection<Tag>> dict)
            return Visibility.Collapsed;

        int n = 3;
        if (parameter is string s && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            n = parsed;

        int count = dict.TryGetValue(trackId, out var coll) ? coll.Count : 0;
        return count > n ? Visibility.Visible : Visibility.Collapsed;
    }

    public object[] ConvertBack(object value, System.Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
