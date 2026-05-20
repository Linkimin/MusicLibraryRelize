using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MusicLibrary.Converters;

/// <summary>
/// Возвращает ActiveBrush, когда value == 0 (или null). Используется в фильтр-popup
/// для пилюли «Все»: она активна, когда MinRating == 0.
/// </summary>
public sealed class ZeroAsActiveBrushConverter : IValueConverter
{
    public Brush ActiveBrush { get; set; } = Brushes.Gold;
    public Brush InactiveBrush { get; set; } = Brushes.Gray;

    public object Convert(object? value, System.Type targetType, object? parameter, CultureInfo culture)
    {
        int n = value is int i ? i : 0;
        return n == 0 ? ActiveBrush : InactiveBrush;
    }

    public object ConvertBack(object? value, System.Type targetType, object? parameter, CultureInfo culture)
        => throw new System.NotSupportedException();
}
