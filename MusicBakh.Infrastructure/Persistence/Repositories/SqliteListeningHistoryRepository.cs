using Microsoft.EntityFrameworkCore;
using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>
/// История прослушиваний поверх LibraryDbContext. Использует IClock, чтобы тесты
/// могли подменять текущее время; в production это SystemClock.
/// </summary>
public sealed class SqliteListeningHistoryRepository : IListeningHistoryRepository
{
    private readonly Func<LibraryDbContext> _contextFactory;
    private readonly IClock _clock;

    public SqliteListeningHistoryRepository(Func<LibraryDbContext> contextFactory, IClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public IReadOnlyList<PlaybackEntry> GetRecent(int limit = 50)
    {
        using var ctx = _contextFactory();
        return ctx.ListeningHistory
            .Include(h => h.Track)
            .OrderByDescending(h => h.PlayedAtUtc)
            .Take(limit)
            .Select(h => new PlaybackEntry
            {
                Track = new Track
                {
                    Id = h.Track.Id,
                    Title = h.Track.Title,
                    Artist = h.Track.Artist,
                    Album = h.Track.Album,
                    Genre = h.Track.Genre,
                    Duration = TimeSpan.FromTicks(h.Track.DurationTicks),
                    FilePath = h.Track.FilePath,
                    CoverPath = h.Track.CoverPath,
                    // Критично: IsBuiltIn должен дотечь до UI. Иначе после перезапуска
                    // history-replay вернёт встроенный трек с IsBuiltIn=false, и кнопка
                    // «Удалить» активируется на shipped-файлы из install-папки.
                    IsBuiltIn = h.Track.IsBuiltIn
                },
                PlayedAt = h.PlayedAtUtc
            })
            .ToList();
    }

    public void Append(PlaybackEntry entry)
    {
        using var ctx = _contextFactory();
        ctx.ListeningHistory.Add(new ListeningHistoryEntryEntity
        {
            TrackId = entry.Track.Id,
            PlayedAtUtc = _clock.UtcNow
        });
        ctx.SaveChanges();
    }
}
