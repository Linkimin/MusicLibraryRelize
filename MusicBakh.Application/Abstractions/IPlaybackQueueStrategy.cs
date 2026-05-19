using MusicBakh.Core.Domain;

namespace MusicBakh.Application.Abstractions;

/// <summary>
/// Решает, какой трек запускается после завершения текущего.
/// Реализации не хранят состояние и используются как singleton'ы.
/// </summary>
public interface IPlaybackQueueStrategy
{
    Track? GetNext(Track current, IReadOnlyList<Track> displayedTracks);
}
