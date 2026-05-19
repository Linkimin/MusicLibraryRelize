using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Хранилище истории прослушиваний. GetRecent остаётся для виджета «недавнее» на
/// MainWindow (последние 50 событий, включая дубли). GetAll/GetTop/GetRecentUnique/
/// GetNeverPlayed появились в итерации B как источник для экрана статистики.
/// </summary>
public interface IListeningHistoryRepository
{
    /// <summary>Последние N событий в хронологическом порядке (новее → старее), с дублями.</summary>
    IReadOnlyList<PlaybackEntry> GetRecent(int limit = 50);

    /// <summary>Все события без лимита — для полного журнала / экспортов.</summary>
    IReadOnlyList<PlaybackEntry> GetAll();

    /// <summary>Топ треков по количеству прослушиваний, сортировка по убыванию счётчика.</summary>
    IReadOnlyList<ListeningStats> GetTop(int limit = 50);

    /// <summary>Последние N уникальных треков (по TrackId) с моментом последнего прослушивания.</summary>
    IReadOnlyList<PlaybackEntry> GetRecentUnique(int limit = 50);

    /// <summary>Треки библиотеки, у которых нет ни одной записи в истории.</summary>
    IReadOnlyList<Track> GetNeverPlayed();

    void Append(PlaybackEntry entry);
}
