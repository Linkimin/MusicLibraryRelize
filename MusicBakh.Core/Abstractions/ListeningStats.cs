using MusicBakh.Core.Domain;

namespace MusicBakh.Core.Abstractions;

/// <summary>
/// Агрегированная запись «топа прослушиваний»: трек, сколько раз он был сыгран
/// и когда последний раз. Возвращается из IListeningHistoryRepository.GetTop().
/// </summary>
public sealed record ListeningStats(Track Track, int PlayCount, DateTime LastPlayedUtc);
