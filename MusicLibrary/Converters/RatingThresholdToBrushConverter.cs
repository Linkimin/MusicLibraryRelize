using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicLibrary.Converters;

/// <summary>
/// Конвертер для «звёздного» рейтинга: при значении value (int Rating, 0..5) и
/// параметре N (string «1»..«5») возвращает «заполненный» цвет, если value ≥ N,
/// иначе «пустой». Brushes передаются через свойства, чтобы XAML мог подсунуть
/// фирменные ресурсы.
/// </summary>
public sealed class RatingThresholdToBrushConverter : IValueConverter
{
    public Brush FilledBrush { get; set; } = Brushes.Gold;
    public Brush EmptyBrush { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int rating = value is int r ? r : 0;
        int threshold = 0;
        if (parameter is string s)
        {
            int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out threshold);
        }
        return rating >= threshold && threshold > 0 ? FilledBrush : EmptyBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
