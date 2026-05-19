using MusicBakh.Core.Abstractions;
using MusicBakh.Core.Domain;
using MusicBakh.Infrastructure.Persistence.Entities;

namespace MusicBakh.Infrastructure.Persistence.Repositories;

/// <summary>
/// Репозиторий треков поверх LibraryDbContext. Получает фабрику контекста, чтобы каждый
/// вызов работал на свежем коротком контексте — это устраняет проблемы с life-time'ом
/// при многопоточности и упрощает тесты.
/// </summary>
public sealed class SqliteTrackRepository : ITrackRepository
{
    private readonly Func<LibraryDbContext> _contextFactory;

    public SqliteTrackRepository(Func<LibraryDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public IReadOnlyList<Track> GetAll()
    {
        using var ctx = _contextFactory();
        return ctx.Tracks
            .OrderBy(t => t.IsBuiltIn ? 0 : 1)
            .ThenBy(t => t.Id)
            .Select(MapToDomain)
            .ToList();
    }

    public Track? FindById(int id)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tracks.FirstOrDefault(t => t.Id == id);
        return entity is null ? null : MapToDomain(entity);
    }

    public Track Add(Track track)
    {
        using var ctx = _contextFactory();
        var entity = new TrackEntity
        {
            Title = track.Title,
            Artist = track.Artist,
            Genre = track.Genre,
            DurationTicks = track.Duration.Ticks,
            FilePath = track.FilePath,
            CoverPath = track.CoverPath,
            AddedAtUtc = DateTime.UtcNow,
            IsBuiltIn = track.IsBuiltIn
        };
        ctx.Tracks.Add(entity);
        ctx.SaveChanges();

        return MapToDomain(entity);
    }

    public void Remove(int id)
    {
        using var ctx = _contextFactory();
        var entity = ctx.Tracks.FirstOrDefault(t => t.Id == id);
        if (entity is null)
        {
            return;
        }
        ctx.Tracks.Remove(entity);
        ctx.SaveChanges();
    }

    private static Track MapToDomain(TrackEntity e) => new()
    {
        Id = e.Id,
        Title = e.Title,
        Artist = e.Artist,
        Genre = e.Genre,
        Duration = TimeSpan.FromTicks(e.DurationTicks),
        FilePath = e.FilePath,
        CoverPath = e.CoverPath,
        IsBuiltIn = e.IsBuiltIn
    };
}
