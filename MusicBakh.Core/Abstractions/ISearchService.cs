using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Полнотекстовый поиск по библиотеке. Реализация выбирает индекс под капотом
/// (FTS5 поверх SQLite, удалённый сервис в будущей фазе B и т.д.). Контракт узкий:
/// одна свободная строка → ранжированный список треков.
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Возвращает треки, отсортированные по релевантности, не более limit штук.
    /// Пустой/whitespace/некорректный запрос возвращает пустой список — никаких
    /// исключений, чтобы UI мог дёргать метод напрямую без try/catch.
    /// </summary>
    IReadOnlyList<Track> Search(string query, int limit = 500);
}
