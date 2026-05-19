using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Хранилище истории прослушиваний. По умолчанию возвращает последние 50 записей,
/// чтобы согласовываться с поведением 1.0.0. В итерации B этот лимит будет снят.
/// </summary>
public interface IListeningHistoryRepository
{
    IReadOnlyList<PlaybackEntry> GetRecent(int limit = 50);

    void Append(PlaybackEntry entry);
}
