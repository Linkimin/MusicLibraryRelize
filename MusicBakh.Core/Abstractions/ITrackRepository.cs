using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Доступ к коллекции треков пользователя. Содержит как встроенные (seed) треки,
/// так и пользовательские. Реализация решает, как они хранятся (в памяти, SQLite, удалённо).
/// </summary>
public interface ITrackRepository
{
    IReadOnlyList<Track> GetAll();

    Track? FindById(int id);

    /// <summary>
    /// Сохраняет новый трек и возвращает его с присвоенным Id.
    /// </summary>
    Track Add(Track track);

    void Remove(int id);
}
