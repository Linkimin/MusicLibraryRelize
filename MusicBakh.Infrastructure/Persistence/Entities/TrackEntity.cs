namespace MusicBakh.Infrastructure.Persistence.Entities;

/// <summary>
/// EF-маппинг трека. Намеренно повторяет поля доменной модели один-в-один, чтобы
/// доменный слой не зависел от EF-аннотаций. Преобразование в Track делает репозиторий.
/// DurationTicks хранится как long, потому что SQLite не поддерживает TimeSpan нативно;
/// ticks дают полный диапазон без потери точности.
/// IsBuiltIn — флаг seed-трека, перенесён из логики CompositeTrackRepository в данные.
/// </summary>
internal sealed class TrackEntity
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
    public string Genre { get; set; } = string.Empty;
    public long DurationTicks { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string CoverPath { get; set; } = string.Empty;
    public DateTime AddedAtUtc { get; set; }
    public bool IsBuiltIn { get; set; }
}
