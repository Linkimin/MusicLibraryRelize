using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Converters;

/// <summary>
/// MultiBinding: values[0] = trackId (int), values[1] = IReadOnlyDictionary&lt;int, ObservableCollection&lt;Tag&gt;&gt;.
/// Возвращает ObservableCollection&lt;Tag&gt; для данного trackId, либо пустой массив, если связки нет.
/// Возвращение именно ObservableCollection нужно, чтобы потребитель (ItemsControl) реагировал
/// на Add/Remove без переустановки ItemsSource.
/// </summary>
public sealed class TrackTagsLookupConverter : IMultiValueConverter
{
    public object Convert(object[] values, System.Type targetType, object? parameter, CultureInfo culture)
    {
        if (values is null || values.Length < 2) return System.Array.Empty<Tag>();
        if (values[0] is not int trackId) return System.Array.Empty<Tag>();
        if (values[1] is not IReadOnlyDictionary<int, System.Collections.ObjectModel.ObservableCollection<Tag>> dict)
            return System.Array.Empty<Tag>();
        return dict.TryGetValue(trackId, out var coll) ? (object)coll : System.Array.Empty<Tag>();
    }

    public object[] ConvertBack(object value, System.Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
