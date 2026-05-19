using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Доступ к коллекции треков пользователя. Содержит как встроенные (seed) треки,
/// так и пользовательские. Реализация решает, как они хранятся (в памяти, SQLite, удалённо).
/// </summary>
public interface ITrackRepository
{
    /// <summary>
    /// Возвращает все треки библиотеки. Порядок реализация определяет сама
    /// (встроенные обычно идут первыми), стабильность гарантируется в пределах одного вызова.
    /// </summary>
    IReadOnlyList<Track> GetAll();

    /// <summary>
    /// Ищет трек по идентификатору. Возвращает null, если трек не найден.
    /// </summary>
    Track? FindById(int id);

    /// <summary>
    /// Сохраняет новый трек и возвращает его с присвоенным Id.
    /// </summary>
    Track Add(Track track);

    /// <summary>
    /// Удаляет трек из библиотеки. Если трека с таким Id нет — тихо ничего не делает.
    /// Удаление встроенных seed-треков не поддерживается и должно бросать NotSupportedException.
    /// </summary>
    void Remove(int id);
}
