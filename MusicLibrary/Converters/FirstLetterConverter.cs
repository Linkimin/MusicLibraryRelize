using System;
using System.Globalization;
using System.Windows.Data;

namespace MusicLibrary.Converters;

/// <summary>Берёт первую букву строки (для аватара исполнителя) в верхнем регистре.</summary>
public sealed class FirstLetterConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s && s.Length > 0) return s[0].ToString().ToUpperInvariant();
        return string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
