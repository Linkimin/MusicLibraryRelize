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
    public string Album { get; init; } = string.Empty;
    public string Genre { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public string CoverPath { get; init; } = string.Empty;

    /// <summary>
    /// Признак встроенного (seed) трека: такие треки нельзя удалять и менять, они
    /// поставляются вместе с приложением. Пользовательские треки имеют IsBuiltIn=false.
    /// </summary>
    public bool IsBuiltIn { get; init; }

    public string DurationText => Duration.ToString(@"m\:ss");
}
