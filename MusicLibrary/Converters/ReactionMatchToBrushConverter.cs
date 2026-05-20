using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using MusicBakh.Core.Domain;

namespace MusicLibrary.Converters;

/// <summary>
/// Конвертер для toggle-кнопок реакции: value — TrackReaction (текущая реакция трека),
/// parameter — строка с именем нужной реакции («Liked»/«Disliked»). Возвращает «активный»
/// цвет, если value == parameter, иначе «выключенный».
/// </summary>
public sealed class ReactionMatchToBrushConverter : IValueConverter
{
    public Brush ActiveBrush { get; set; } = Brushes.Gold;
    public Brush InactiveBrush { get; set; } = Brushes.Gray;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TrackReaction current || parameter is not string target)
        {
            return InactiveBrush;
        }
        if (!Enum.TryParse<TrackReaction>(target, ignoreCase: true, out var wanted))
        {
            return InactiveBrush;
        }
        return current == wanted ? ActiveBrush : InactiveBrush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
