using MusicBakh.Application.Contracts;

namespace MusicBakh.Application.Abstractions;

/// <summary>
/// Клиент REST API MusicBrainz для уточнения исполнителя и жанра по названию записи.
/// Возвращает null если ничего не найдено или внешний сервис недоступен —
/// это не ошибка, импорт продолжается с тем что есть.
/// </summary>
public interface IMusicBrainzClient
{
    Task<MusicBrainzMatch?> SearchAsync(string artist, string title, CancellationToken cancellationToken);
}
