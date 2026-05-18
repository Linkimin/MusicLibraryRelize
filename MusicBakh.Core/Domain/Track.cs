namespace MusicBakh.Core.Domain;

/// <summary>
/// Описывает музыкальный трек из локальной библиотеки.
/// Модель не зависит от инфраструктуры, чтобы данные можно было использовать в любом слое.
/// </summary>
public sealed class Track
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Artist { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string CoverPath { get; init; } = string.Empty;

    public string DurationText => Duration.ToString(@"m\:ss");
}
